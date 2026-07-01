using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.eShopWeb.Infrastructure.Services.Maxio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Microsoft.eShopWeb.Infrastructure;

/// <summary>
/// Wires the single Maxio integration point (plan.md section 2.2/4.3). Shared by both hosts so the
/// typed client, auth, and resilience pipeline are configured exactly once (api-integration-quality-gate.md Gate 0).
/// </summary>
public static class MaxioDependencies
{
    public static IServiceCollection AddMaxioBillingClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MaxioSettings>()
            .Bind(configuration.GetSection("Maxio"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IBillingClient, MaxioBillingClient>((provider, client) =>
            {
                var settings = provider.GetRequiredService<IOptions<MaxioSettings>>().Value;
                client.BaseAddress = new Uri(settings.ResolveBaseUrl());
                client.Timeout = Timeout.InfiniteTimeSpan; // the resilience pipeline below owns per-attempt timing, not the client-wide timeout.

                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.ApiKey}:x"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddResilienceHandler("maxio", builder =>
            {
                var retryOptions = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(500)
                };
                // Maxio declares no idempotency key for any mutating operation it exposes (see
                // api-integration-quality-gate.md Gate 3) - retrying a POST/PUT/DELETE on a transient
                // failure risks a duplicate real-world billing side effect. Only safe (GET) requests
                // are retried automatically; a failed write surfaces immediately as BillingProviderException.
                retryOptions.DisableForUnsafeHttpMethods();
                builder.AddRetry(retryOptions);

                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(15)
                });

                // Added last so it wraps each individual attempt, not the retry loop as a whole.
                builder.AddTimeout(TimeSpan.FromSeconds(10));
            });

        return services;
    }
}
