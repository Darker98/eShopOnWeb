using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Use-case surface for the subscription module (mirrors IOrderService / BasketService): validates,
/// calls the billing client, persists the userId&lt;-&gt;provider mapping, and publishes MediatR notifications.
/// </summary>
public interface ISubscriptionService
{
    Task<IReadOnlyList<BillingPlan>> ListPlansAsync(CancellationToken cancellationToken);

    /// <summary>UC1: ensures a provider customer exists for <paramref name="buyerId"/>, then enrolls it in <paramref name="productHandle"/>.</summary>
    Task<SubscriptionSummary> SubscribeAsync(string buyerId, string email, string firstName, string lastName, string productHandle, string? paymentToken, CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSummary>> GetSubscriptionsForUserAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>Throws SubscriptionNotFoundException if the subscription does not exist, or belongs to someone else and <paramref name="isAdmin"/> is false.</summary>
    Task<SubscriptionSummary> GetSubscriptionAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken);

    /// <summary>UC2: records usage idempotently on <paramref name="idempotencyKey"/>, after verifying the configured component is metered.</summary>
    Task<UsageSummary> RecordUsageAsync(string actorBuyerId, bool isAdmin, int subscriptionId, decimal quantity, string? memo, string idempotencyKey, CancellationToken cancellationToken);

    Task<int> GetUsageBalanceAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken);

    /// <summary>UC3 preview: the prorated cost of moving to <paramref name="targetProductHandle"/> right now. AtRenewal has no proration to preview.</summary>
    Task<BillingProrationPreview?> PreviewPlanChangeAsync(string actorBuyerId, bool isAdmin, int subscriptionId, string targetProductHandle, PlanChangeTiming timing, CancellationToken cancellationToken);

    /// <summary>
    /// UC3 commit. When <paramref name="timing"/> is Now and <paramref name="expectedProratedAdjustmentInCents"/> is supplied,
    /// the service re-previews immediately before committing and throws StalePlanChangePreviewException on mismatch (AC-07b).
    /// </summary>
    Task<PlanChangeResult> CommitPlanChangeAsync(string actorBuyerId, bool isAdmin, int subscriptionId, string targetProductHandle, PlanChangeTiming timing, int? expectedProratedAdjustmentInCents, CancellationToken cancellationToken);

    Task<SubscriptionSummary> PauseAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken);

    Task<SubscriptionSummary> ResumeAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken);

    Task<SubscriptionSummary> CancelAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancelTiming timing, string? reason, CancellationToken cancellationToken);

    Task<SubscriptionSummary> ReactivateAsync(string actorBuyerId, bool isAdmin, int subscriptionId, CancellationToken cancellationToken);
}
