using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Provider-agnostic view of a recurring plan a customer can subscribe to.
/// Money is kept in integer cents, matching Maxio's own representation, so no
/// precision is lost converting to/from a decimal before it reaches a view.
/// </summary>
public record BillingPlan(
    string Handle,
    int ProviderProductId,
    string Name,
    int PriceInCents,
    int IntervalCount,
    string IntervalUnit,
    bool RequiresPaymentMethod);

/// <summary>Provider-agnostic view of a metered/quantity/on-off component on the product family.</summary>
public record BillingComponentInfo(
    int ProviderComponentId,
    string Handle,
    string Name,
    string Kind,
    bool IsMetered);

/// <summary>Provider-agnostic view of a subscription's current billing state.</summary>
public record BillingSubscription(
    int ProviderSubscriptionId,
    int ProviderCustomerId,
    string ProductHandle,
    string State,
    int BalanceInCents,
    DateTimeOffset? CurrentPeriodEndsAt,
    DateTimeOffset? NextAssessmentAt,
    bool? CancelAtEndOfPeriod,
    DateTimeOffset? DelayedCancelAt,
    DateTimeOffset? CanceledAt);

/// <summary>Result of recording a unit of metered usage. ProviderUsageId is long: Usage-Response.yaml declares id as int64, unlike every other Maxio resource id.</summary>
public record BillingUsageResult(long ProviderUsageId, decimal Quantity, string? Memo);

/// <summary>
/// The prorated cost of moving a subscription to a different plan, as computed by the provider.
/// Every field mirrors Subscription-Migration-Preview.yaml exactly (all amounts in cents).
/// </summary>
public record BillingProrationPreview(
    int ProratedAdjustmentInCents,
    int ChargeInCents,
    int PaymentDueInCents,
    int CreditAppliedInCents);

/// <summary>Typed, per-operation view of a Maxio error response body (see Error-List-Response.yaml / Single-Error-Response.yaml).</summary>
public record BillingProviderError(IReadOnlyList<string> Messages)
{
    public static readonly BillingProviderError Empty = new(Array.Empty<string>());
}

public enum PlanChangeTiming
{
    /// <summary>Commit through migrations.json with preserve_period=true: prorated charge/credit applied immediately.</summary>
    Now,

    /// <summary>Commit through the delayed product change (product_change_delayed=true): takes effect at next renewal, no proration.</summary>
    AtRenewal
}

public enum CancelTiming
{
    Immediate,
    EndOfPeriod
}
