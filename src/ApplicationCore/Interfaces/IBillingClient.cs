using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Provider-agnostic seam onto the recurring-billing provider. This is the ONLY interface
/// ApplicationCore knows about; the concrete implementation (Maxio, over plain HTTP) lives in
/// Infrastructure. No Maxio-specific type, status code, or field name leaks through this contract.
/// </summary>
public interface IBillingClient
{
    Task<IReadOnlyList<BillingPlan>> ListPlansAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the configured usage component handle resolves to a component of metered kind on the
    /// product family (UC2 startup/first-call check) and returns its info. Throws
    /// MeteredComponentMisconfiguredException otherwise - the component handle itself is a Maxio-side
    /// configuration detail Infrastructure owns; ApplicationCore never needs to know it.
    /// </summary>
    Task<BillingComponentInfo> GetMeteredComponentAsync(CancellationToken cancellationToken);

    /// <summary>Idempotent on <paramref name="customerReference"/>: returns the existing provider customer id if one is already on file.</summary>
    Task<int> EnsureCustomerAsync(string customerReference, string email, string firstName, string lastName, CancellationToken cancellationToken);

    Task<BillingSubscription> CreateSubscriptionAsync(int providerCustomerId, string productHandle, string? paymentToken, CancellationToken cancellationToken);

    Task<BillingSubscription> GetSubscriptionAsync(int providerSubscriptionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BillingSubscription>> ListCustomerSubscriptionsAsync(int providerCustomerId, CancellationToken cancellationToken);

    /// <summary>Records usage against the one configured metered component. Validates the component's kind first (see GetMeteredComponentAsync).</summary>
    Task<BillingUsageResult> RecordUsageAsync(int providerSubscriptionId, decimal quantity, string? memo, CancellationToken cancellationToken);

    /// <summary>Period-to-date unit balance for the configured metered component on a subscription.</summary>
    Task<int> GetUsageBalanceAsync(int providerSubscriptionId, CancellationToken cancellationToken);

    Task<BillingProrationPreview> PreviewPlanChangeNowAsync(int providerSubscriptionId, string targetProductHandle, CancellationToken cancellationToken);

    Task<BillingSubscription> CommitPlanChangeNowAsync(int providerSubscriptionId, string targetProductHandle, CancellationToken cancellationToken);

    Task<BillingSubscription> SchedulePlanChangeAtRenewalAsync(int providerSubscriptionId, string targetProductHandle, CancellationToken cancellationToken);

    Task<BillingSubscription> PauseSubscriptionAsync(int providerSubscriptionId, CancellationToken cancellationToken);

    Task<BillingSubscription> ResumeSubscriptionAsync(int providerSubscriptionId, CancellationToken cancellationToken);

    Task<BillingSubscription> CancelSubscriptionImmediatelyAsync(int providerSubscriptionId, string? reason, CancellationToken cancellationToken);

    Task<BillingSubscription> ScheduleCancelAtEndOfPeriodAsync(int providerSubscriptionId, string? reason, CancellationToken cancellationToken);

    Task<BillingSubscription> ReactivateSubscriptionAsync(int providerSubscriptionId, CancellationToken cancellationToken);
}
