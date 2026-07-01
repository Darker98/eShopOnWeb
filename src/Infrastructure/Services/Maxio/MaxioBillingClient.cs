using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services.Maxio;

/// <summary>
/// The single point of contact with Maxio Advanced Billing (plan.md section 2.2 / 4.2): plain HTTP
/// over a typed HttpClient, no vendor SDK. Every method maps 1:1 to one named operation in
/// Specification/openapi.yaml; see the XML doc on each for the exact path + operationId.
/// </summary>
public class MaxioBillingClient : IBillingClient
{
    private readonly HttpClient _httpClient;
    private readonly MaxioSettings _settings;
    private readonly ILogger<MaxioBillingClient> _logger;

    public MaxioBillingClient(HttpClient httpClient, IOptions<MaxioSettings> settings, ILogger<MaxioBillingClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>GET /product_families/{product_family_id}/products.json (listProductsForProductFamily)</summary>
    public async Task<IReadOnlyList<BillingPlan>> ListPlansAsync(CancellationToken cancellationToken)
    {
        var familyRoute = FamilyRoute();
        var products = await SendAsync<List<ProductResponseDto>>(HttpMethod.Get, $"product_families/{familyRoute}/products.json", null, Success200, cancellationToken);

        return products
            .Select(p => p.Product)
            .Select(p => new BillingPlan(p.Handle, p.Id, p.Name, (int)p.PriceInCents, p.Interval, p.IntervalUnit, p.RequireCreditCard))
            .ToList();
    }

    /// <summary>GET /product_families/{product_family_id}/components/{component_id}.json (readComponent)</summary>
    public async Task<BillingComponentInfo> GetMeteredComponentAsync(CancellationToken cancellationToken)
    {
        var familyRoute = FamilyRoute();
        var componentRoute = Uri.EscapeDataString($"handle:{_settings.MeteredComponentHandle}");
        var response = await SendAsync<ComponentResponseDto>(HttpMethod.Get, $"product_families/{familyRoute}/components/{componentRoute}.json", null, Success200, cancellationToken);

        var component = response.Component;
        var isMetered = component.Kind == "metered_component";
        if (!isMetered)
        {
            throw new MeteredComponentMisconfiguredException(_settings.MeteredComponentHandle, component.Kind);
        }

        return new BillingComponentInfo(component.Id, _settings.MeteredComponentHandle, component.Name, component.Kind, isMetered);
    }

    /// <summary>GET /customers/lookup.json (readCustomerByReference), falling back to POST /customers.json (createCustomer).</summary>
    public async Task<int> EnsureCustomerAsync(string customerReference, string email, string firstName, string lastName, CancellationToken cancellationToken)
    {
        var reference = Uri.EscapeDataString(customerReference);
        using var lookupRequest = new HttpRequestMessage(HttpMethod.Get, $"customers/lookup.json?reference={reference}");
        using var lookupResponse = await _httpClient.SendAsync(lookupRequest, cancellationToken);

        if (lookupResponse.StatusCode == HttpStatusCode.OK)
        {
            var body = await lookupResponse.Content.ReadAsStringAsync(cancellationToken);
            var existing = JsonSerializer.Deserialize<CustomerResponseDto>(body, MaxioJson.Options);
            if (existing is not null)
            {
                return existing.Customer.Id;
            }
        }
        else if (lookupResponse.StatusCode != HttpStatusCode.NotFound)
        {
            var body = await lookupResponse.Content.ReadAsStringAsync(cancellationToken);
            await ThrowForUnexpectedStatusAsync(HttpMethod.Get, "customers/lookup.json", lookupResponse.StatusCode, body);
        }

        var created = await SendAsync<CustomerResponseDto>(HttpMethod.Post, "customers.json", new CreateCustomerRequestDto
        {
            Customer = new CustomerAttributesForCreateDto
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Reference = customerReference
            }
        }, Success200, cancellationToken);

        return created.Customer.Id;
    }

    /// <summary>POST /subscriptions.json (createSubscription)</summary>
    public async Task<BillingSubscription> CreateSubscriptionAsync(int providerCustomerId, string productHandle, string? paymentToken, CancellationToken cancellationToken)
    {
        var request = new CreateSubscriptionRequestDto
        {
            Subscription = new CreateSubscriptionAttributesDto
            {
                ProductHandle = productHandle,
                CustomerId = providerCustomerId,
                PaymentProfileAttributes = paymentToken is null ? null : new PaymentProfileAttributesForCreateDto { ChargifyToken = paymentToken },
                // Without a payment method, "automatic" collection (the default) would try to charge a
                // card that doesn't exist and fail with "no payment method on file for the $X balance".
                // "remittance" defers collection instead, matching the demo plans' require_credit_card=false.
                PaymentCollectionMethod = paymentToken is null ? "remittance" : null
            }
        };

        var response = await SendAsync<SubscriptionResponseDto>(HttpMethod.Post, "subscriptions.json", request, Success201, cancellationToken);
        return ToBillingSubscription(response.Subscription);
    }

    /// <summary>GET /subscriptions/{subscription_id}.json (readSubscription)</summary>
    public async Task<BillingSubscription> GetSubscriptionAsync(int providerSubscriptionId, CancellationToken cancellationToken)
    {
        var response = await SendAsync<SubscriptionResponseDto>(HttpMethod.Get, $"subscriptions/{providerSubscriptionId}.json", null, Success200, cancellationToken);
        return ToBillingSubscription(response.Subscription);
    }

    /// <summary>GET /customers/{customer_id}/subscriptions.json (listCustomerSubscriptions)</summary>
    public async Task<IReadOnlyList<BillingSubscription>> ListCustomerSubscriptionsAsync(int providerCustomerId, CancellationToken cancellationToken)
    {
        var response = await SendAsync<List<SubscriptionResponseDto>>(HttpMethod.Get, $"customers/{providerCustomerId}/subscriptions.json", null, Success200, cancellationToken);
        return response.Select(r => ToBillingSubscription(r.Subscription)).ToList();
    }

    /// <summary>POST /subscriptions/{subscription_id_or_reference}/components/{component_id}/usages.json (createUsage)</summary>
    public async Task<BillingUsageResult> RecordUsageAsync(int providerSubscriptionId, decimal quantity, string? memo, CancellationToken cancellationToken)
    {
        await GetMeteredComponentAsync(cancellationToken); // refuses to proceed if misconfigured - see plan.md UC2 preconditions.

        var componentRoute = Uri.EscapeDataString($"handle:{_settings.MeteredComponentHandle}");
        var request = new CreateUsageRequestDto { Usage = new CreateUsageAttributesDto { Quantity = quantity, Memo = memo } };
        var response = await SendAsync<UsageResponseDto>(HttpMethod.Post, $"subscriptions/{providerSubscriptionId}/components/{componentRoute}/usages.json", request, Success200, cancellationToken);

        return new BillingUsageResult(response.Usage.Id, response.Usage.Quantity, response.Usage.Memo);
    }

    /// <summary>GET /subscriptions/{subscription_id}/components/{component_id}.json (readSubscriptionComponent)</summary>
    public async Task<int> GetUsageBalanceAsync(int providerSubscriptionId, CancellationToken cancellationToken)
    {
        var componentRoute = Uri.EscapeDataString($"handle:{_settings.MeteredComponentHandle}");
        var response = await SendAsync<SubscriptionComponentResponseDto>(HttpMethod.Get, $"subscriptions/{providerSubscriptionId}/components/{componentRoute}.json", null, Success200, cancellationToken);
        return response.Component.UnitBalance;
    }

    /// <summary>POST /subscriptions/{subscription_id}/migrations/preview.json (previewSubscriptionProductMigration)</summary>
    public async Task<BillingProrationPreview> PreviewPlanChangeNowAsync(int providerSubscriptionId, string targetProductHandle, CancellationToken cancellationToken)
    {
        var request = new MigrationRequestDto { Migration = new MigrationAttributesDto { ProductHandle = targetProductHandle, PreservePeriod = true } };
        var response = await SendAsync<MigrationPreviewResponseDto>(HttpMethod.Post, $"subscriptions/{providerSubscriptionId}/migrations/preview.json", request, Success200, cancellationToken);

        var preview = response.Migration;
        return new BillingProrationPreview(preview.ProratedAdjustmentInCents, preview.ChargeInCents, preview.PaymentDueInCents, preview.CreditAppliedInCents);
    }

    /// <summary>POST /subscriptions/{subscription_id}/migrations.json (migrateSubscriptionProduct), preserve_period=true for an immediate prorated change.</summary>
    public async Task<BillingSubscription> CommitPlanChangeNowAsync(int providerSubscriptionId, string targetProductHandle, CancellationToken cancellationToken)
    {
        var request = new MigrationRequestDto { Migration = new MigrationAttributesDto { ProductHandle = targetProductHandle, PreservePeriod = true } };
        var response = await SendAsync<SubscriptionResponseDto>(HttpMethod.Post, $"subscriptions/{providerSubscriptionId}/migrations.json", request, Success200, cancellationToken);
        return ToBillingSubscription(response.Subscription);
    }

    /// <summary>PUT /subscriptions/{subscription_id}.json (updateSubscription) with product_change_delayed=true: takes effect at next renewal, no proration.</summary>
    public async Task<BillingSubscription> SchedulePlanChangeAtRenewalAsync(int providerSubscriptionId, string targetProductHandle, CancellationToken cancellationToken)
    {
        var request = new UpdateSubscriptionRequestDto { Subscription = new UpdateSubscriptionAttributesDto { ProductHandle = targetProductHandle, ProductChangeDelayed = true } };
        var response = await SendAsync<SubscriptionResponseDto>(HttpMethod.Put, $"subscriptions/{providerSubscriptionId}.json", request, Success200, cancellationToken);
        return ToBillingSubscription(response.Subscription);
    }

    /// <summary>POST /subscriptions/{subscription_id}/hold.json (pauseSubscription)</summary>
    public async Task<BillingSubscription> PauseSubscriptionAsync(int providerSubscriptionId, CancellationToken cancellationToken)
    {
        var response = await SendAsync<SubscriptionResponseDto>(HttpMethod.Post, $"subscriptions/{providerSubscriptionId}/hold.json", new PauseRequestDto(), Success200, cancellationToken);
        return ToBillingSubscription(response.Subscription);
    }

    /// <summary>POST /subscriptions/{subscription_id}/resume.json (resumeSubscription)</summary>
    public async Task<BillingSubscription> ResumeSubscriptionAsync(int providerSubscriptionId, CancellationToken cancellationToken)
    {
        var response = await SendAsync<SubscriptionResponseDto>(HttpMethod.Post, $"subscriptions/{providerSubscriptionId}/resume.json", null, Success200, cancellationToken);
        return ToBillingSubscription(response.Subscription);
    }

    /// <summary>DELETE /subscriptions/{subscription_id}.json (cancelSubscription)</summary>
    public async Task<BillingSubscription> CancelSubscriptionImmediatelyAsync(int providerSubscriptionId, string? reason, CancellationToken cancellationToken)
    {
        var request = new CancellationRequestDto { Subscription = new CancellationAttributesDto { CancellationMessage = reason } };
        var response = await SendAsync<SubscriptionResponseDto>(HttpMethod.Delete, $"subscriptions/{providerSubscriptionId}.json", request, Success200, cancellationToken);
        return ToBillingSubscription(response.Subscription);
    }

    /// <summary>POST /subscriptions/{subscription_id}/delayed_cancel.json (initiateDelayedCancellation), then re-reads the subscription for its fresh state.</summary>
    public async Task<BillingSubscription> ScheduleCancelAtEndOfPeriodAsync(int providerSubscriptionId, string? reason, CancellationToken cancellationToken)
    {
        var request = new CancellationRequestDto { Subscription = new CancellationAttributesDto { CancellationMessage = reason } };
        await SendAsync<object>(HttpMethod.Post, $"subscriptions/{providerSubscriptionId}/delayed_cancel.json", request, Success200, cancellationToken);

        // Delayed-Cancellation-Response only carries a message, not the subscription snapshot.
        return await GetSubscriptionAsync(providerSubscriptionId, cancellationToken);
    }

    /// <summary>PUT /subscriptions/{subscription_id}/reactivate.json (reactivateSubscription)</summary>
    public async Task<BillingSubscription> ReactivateSubscriptionAsync(int providerSubscriptionId, CancellationToken cancellationToken)
    {
        var response = await SendAsync<SubscriptionResponseDto>(HttpMethod.Put, $"subscriptions/{providerSubscriptionId}/reactivate.json", new { }, Success200, cancellationToken);
        return ToBillingSubscription(response.Subscription);
    }

    private string FamilyRoute() => Uri.EscapeDataString($"handle:{_settings.ProductFamilyHandle}");

    private static BillingSubscription ToBillingSubscription(SubscriptionDto dto) => new(
        dto.Id,
        dto.Customer?.Id ?? 0,
        dto.Product?.Handle ?? string.Empty,
        dto.State,
        (int)dto.BalanceInCents,
        dto.CurrentPeriodEndsAt,
        dto.NextAssessmentAt,
        dto.CancelAtEndOfPeriod,
        dto.DelayedCancelAt,
        dto.CanceledAt);

    private static readonly HashSet<int> Success200 = new() { 200 };
    private static readonly HashSet<int> Success201 = new() { 201 };

    private async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string path, object? body, HashSet<int> successStatusCodes, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: MaxioJson.Options);
            }

            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Maxio {Method} {Path} failed before a response was received", method, path);
            throw new BillingProviderException("The billing provider is currently unavailable. Please try again shortly.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Maxio {Method} {Path} timed out", method, path);
            throw new BillingProviderException("The billing provider is currently unavailable. Please try again shortly.");
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (successStatusCodes.Contains((int)response.StatusCode))
            {
                if (typeof(TResponse) == typeof(object))
                {
                    return default!;
                }

                return JsonSerializer.Deserialize<TResponse>(responseBody, MaxioJson.Options)
                    ?? throw new BillingProviderException("The billing provider returned an empty response.");
            }

            await ThrowForUnexpectedStatusAsync(method, path, response.StatusCode, responseBody);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private Task ThrowForUnexpectedStatusAsync(HttpMethod method, string path, HttpStatusCode statusCode, string responseBody)
    {
        var messages = MaxioErrorReader.ExtractMessages(responseBody);
        _logger.LogWarning("Maxio {Method} {Path} returned {StatusCode}: {Messages}", method, path, (int)statusCode, string.Join("; ", messages));

        var safeMessage = (int)statusCode is >= 400 and < 500
            ? (messages.Count > 0 ? string.Join(" ", messages) : $"The billing provider rejected the request (HTTP {(int)statusCode}).")
            : "The billing provider is currently unavailable. Please try again shortly.";

        throw new BillingProviderException(safeMessage, messages, (int)statusCode);
    }
}
