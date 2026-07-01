using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IRepository<Subscription> _subscriptionRepository;
    private readonly IRepository<UsageRecord> _usageRepository;
    private readonly IBillingClient _billingClient;
    private readonly IPublisher _publisher;
    private readonly IAppLogger<SubscriptionService> _logger;

    public SubscriptionService(
        IRepository<Subscription> subscriptionRepository,
        IRepository<UsageRecord> usageRepository,
        IBillingClient billingClient,
        IPublisher publisher,
        IAppLogger<SubscriptionService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _usageRepository = usageRepository;
        _billingClient = billingClient;
        _publisher = publisher;
        _logger = logger;
    }

    public Task<IReadOnlyList<BillingPlan>> ListPlansAsync(CancellationToken cancellationToken)
        => _billingClient.ListPlansAsync(cancellationToken);

    public async Task<SubscriptionSummary> SubscribeAsync(string buyerId, string email, string firstName, string lastName, string productHandle, string? paymentToken, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(email, nameof(email));
        Guard.Against.NullOrEmpty(firstName, nameof(firstName));
        Guard.Against.NullOrEmpty(lastName, nameof(lastName));
        Guard.Against.NullOrEmpty(productHandle, nameof(productHandle));

        var plans = await _billingClient.ListPlansAsync(cancellationToken);
        var plan = plans.FirstOrDefault(p => p.Handle == productHandle);
        if (plan is null)
        {
            throw new InvalidSubscriptionStateException($"'{productHandle}' is not a plan available for subscription.");
        }
        if (plan.RequiresPaymentMethod && string.IsNullOrEmpty(paymentToken))
        {
            throw new InvalidSubscriptionStateException($"Plan '{productHandle}' requires a payment method; none was provided.");
        }

        var providerCustomerId = await _billingClient.EnsureCustomerAsync(buyerId, email, firstName, lastName, cancellationToken);
        var billingSubscription = await _billingClient.CreateSubscriptionAsync(providerCustomerId, productHandle, paymentToken, cancellationToken);

        var subscription = new Subscription(buyerId, providerCustomerId, billingSubscription.ProviderSubscriptionId, productHandle, billingSubscription.State);
        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        await PublishBestEffortAsync(new SubscriptionActivated(buyerId, subscription.Id, subscription.ProviderSubscriptionId, productHandle), cancellationToken);

        return new SubscriptionSummary(subscription.Id, billingSubscription);
    }

    public async Task<IReadOnlyList<SubscriptionSummary>> GetSubscriptionsForUserAsync(string buyerId, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var subscriptions = await _subscriptionRepository.ListAsync(new SubscriptionsByUserSpecification(buyerId), cancellationToken);
        var summaries = new List<SubscriptionSummary>(subscriptions.Count);
        foreach (var subscription in subscriptions)
        {
            summaries.Add(await RefreshFromProviderAsync(subscription, cancellationToken));
        }

        return summaries;
    }

    public async Task<SubscriptionSummary> GetSubscriptionAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);
        return await RefreshFromProviderAsync(subscription, cancellationToken);
    }

    public async Task<UsageSummary> RecordUsageAsync(string actorBuyerId, bool isAdmin, int subscriptionId, decimal quantity, string? memo, string idempotencyKey, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));
        if (quantity == 0)
        {
            throw new InvalidSubscriptionStateException("Usage quantity must not be zero.");
        }

        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);

        var existing = await _usageRepository.FirstOrDefaultAsync(
            new UsageRecordByIdempotencyKeySpecification(subscription.ProviderSubscriptionId, idempotencyKey), cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Duplicate usage report for subscription {SubscriptionId} with idempotency key {IdempotencyKey} - returning the original result", subscriptionId, idempotencyKey);
            var currentBalance = await _billingClient.GetUsageBalanceAsync(subscription.ProviderSubscriptionId, cancellationToken);
            return new UsageSummary(existing.ProviderUsageId, existing.Quantity, currentBalance);
        }

        var result = await _billingClient.RecordUsageAsync(subscription.ProviderSubscriptionId, quantity, memo, cancellationToken);
        await _usageRepository.AddAsync(new UsageRecord(subscription.ProviderSubscriptionId, idempotencyKey, result.Quantity, result.Memo, result.ProviderUsageId), cancellationToken);

        var balance = await _billingClient.GetUsageBalanceAsync(subscription.ProviderSubscriptionId, cancellationToken);
        return new UsageSummary(result.ProviderUsageId, result.Quantity, balance);
    }

    public async Task<int> GetUsageBalanceAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);
        return await _billingClient.GetUsageBalanceAsync(subscription.ProviderSubscriptionId, cancellationToken);
    }

    public async Task<BillingProrationPreview?> PreviewPlanChangeAsync(string actorBuyerId, bool isAdmin, int subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken)
    {
        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);
        GuardTargetPlanIsDifferent(subscription, targetProductHandle);

        return timing == PlanChangeTiming.Now
            ? await _billingClient.PreviewPlanChangeNowAsync(subscription.ProviderSubscriptionId, targetProductHandle, cancellationToken)
            : null; // AtRenewal is a delayed product change: no proration applies.
    }

    public async Task<PlanChangeResult> CommitPlanChangeAsync(string actorBuyerId, bool isAdmin, int subscriptionId, string targetProductHandle, PlanChangeTiming timing, int? expectedProratedAdjustmentInCents, CancellationToken cancellationToken)
    {
        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);
        GuardTargetPlanIsDifferent(subscription, targetProductHandle);

        var oldProductHandle = subscription.ProductHandle;
        BillingProrationPreview? proration = null;
        BillingSubscription updated;

        if (timing == PlanChangeTiming.Now)
        {
            proration = await _billingClient.PreviewPlanChangeNowAsync(subscription.ProviderSubscriptionId, targetProductHandle, cancellationToken);
            if (expectedProratedAdjustmentInCents.HasValue && expectedProratedAdjustmentInCents.Value != proration.ProratedAdjustmentInCents)
            {
                throw new StalePlanChangePreviewException();
            }

            updated = await _billingClient.CommitPlanChangeNowAsync(subscription.ProviderSubscriptionId, targetProductHandle, cancellationToken);
        }
        else
        {
            updated = await _billingClient.SchedulePlanChangeAtRenewalAsync(subscription.ProviderSubscriptionId, targetProductHandle, cancellationToken);
        }

        subscription.SyncFromProvider(updated.ProductHandle, updated.State);
        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        await PublishBestEffortAsync(new SubscriptionPlanChanged(subscription.BuyerId, subscription.Id, subscription.ProviderSubscriptionId, oldProductHandle, targetProductHandle, timing), cancellationToken);

        return new PlanChangeResult(new SubscriptionSummary(subscription.Id, updated), oldProductHandle, targetProductHandle, timing, proration);
    }

    public async Task<SubscriptionSummary> PauseAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);
        if (subscription.State == "on_hold")
        {
            throw new InvalidSubscriptionStateException("Subscription is already on hold.");
        }

        return await ApplyTransitionAsync(subscription, () => _billingClient.PauseSubscriptionAsync(subscription.ProviderSubscriptionId, cancellationToken), cancellationToken);
    }

    public async Task<SubscriptionSummary> ResumeAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);
        if (subscription.State != "on_hold")
        {
            throw new InvalidSubscriptionStateException("Only subscriptions on hold can be resumed.");
        }

        return await ApplyTransitionAsync(subscription, () => _billingClient.ResumeSubscriptionAsync(subscription.ProviderSubscriptionId, cancellationToken), cancellationToken);
    }

    public async Task<SubscriptionSummary> CancelAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancelTiming timing, string? reason, CancellationToken cancellationToken)
    {
        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);
        if (subscription.State is "canceled" or "expired")
        {
            throw new InvalidSubscriptionStateException("Subscription is already canceled.");
        }

        return await ApplyTransitionAsync(subscription, () => timing == CancelTiming.Immediate
            ? _billingClient.CancelSubscriptionImmediatelyAsync(subscription.ProviderSubscriptionId, reason, cancellationToken)
            : _billingClient.ScheduleCancelAtEndOfPeriodAsync(subscription.ProviderSubscriptionId, reason, cancellationToken), cancellationToken);
    }

    public async Task<SubscriptionSummary> ReactivateAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await LoadOwnedSubscriptionAsync(actorBuyerId, isAdmin, subscriptionId, cancellationToken);
        if (subscription.State is not ("canceled" or "unpaid" or "trial_ended"))
        {
            throw new InvalidSubscriptionStateException("Only canceled, unpaid, or trial-ended subscriptions can be reactivated.");
        }

        return await ApplyTransitionAsync(subscription, () => _billingClient.ReactivateSubscriptionAsync(subscription.ProviderSubscriptionId, cancellationToken), cancellationToken);
    }

    private async Task<SubscriptionSummary> ApplyTransitionAsync(Subscription subscription, Func<Task<BillingSubscription>> transition, CancellationToken cancellationToken)
    {
        var oldState = subscription.State;
        var updated = await transition();

        subscription.SyncFromProvider(updated.ProductHandle, updated.State);
        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        await PublishBestEffortAsync(new SubscriptionStateChanged(subscription.BuyerId, subscription.Id, subscription.ProviderSubscriptionId, oldState, updated.State), cancellationToken);

        return new SubscriptionSummary(subscription.Id, updated);
    }

    private async Task<Subscription> LoadOwnedSubscriptionAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(actorBuyerId, nameof(actorBuyerId));

        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription is null || (!isAdmin && subscription.BuyerId != actorBuyerId))
        {
            // Same exception for "not found" and "not yours": ownership is never revealed to a non-owner.
            throw new SubscriptionNotFoundException(subscriptionId);
        }

        return subscription;
    }

    private async Task<SubscriptionSummary> RefreshFromProviderAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        var billingSubscription = await _billingClient.GetSubscriptionAsync(subscription.ProviderSubscriptionId, cancellationToken);
        subscription.SyncFromProvider(billingSubscription.ProductHandle, billingSubscription.State);
        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        return new SubscriptionSummary(subscription.Id, billingSubscription);
    }

    private static void GuardTargetPlanIsDifferent(Subscription subscription, string targetProductHandle)
    {
        Guard.Against.NullOrEmpty(targetProductHandle, nameof(targetProductHandle));
        if (subscription.ProductHandle == targetProductHandle)
        {
            throw new InvalidSubscriptionStateException($"Subscription is already on plan '{targetProductHandle}'.");
        }
    }

    private async Task PublishBestEffortAsync(INotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.Publish(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            // In-process notifications are best-effort (plan.md section 2.5): a handler failure must
            // never roll back the billing-provider action that already succeeded.
            _logger.LogWarning("In-process notification handler failed for {NotificationType}: {Message}", notification.GetType().Name, ex.Message);
        }
    }
}
