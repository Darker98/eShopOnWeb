using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>Flat wire projection of a SubscriptionSummary (mirrors CatalogItemDto).</summary>
public class SubscriptionDto
{
    public int SubscriptionId { get; set; }
    public int ProviderSubscriptionId { get; set; }
    public string ProductHandle { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int BalanceInCents { get; set; }
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public DateTimeOffset? NextAssessmentAt { get; set; }
    public bool? CancelAtEndOfPeriod { get; set; }
    public DateTimeOffset? DelayedCancelAt { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }

    public static SubscriptionDto FromSummary(SubscriptionSummary summary) => new()
    {
        SubscriptionId = summary.SubscriptionId,
        ProviderSubscriptionId = summary.Provider.ProviderSubscriptionId,
        ProductHandle = summary.Provider.ProductHandle,
        State = summary.Provider.State,
        BalanceInCents = summary.Provider.BalanceInCents,
        CurrentPeriodEndsAt = summary.Provider.CurrentPeriodEndsAt,
        NextAssessmentAt = summary.Provider.NextAssessmentAt,
        CancelAtEndOfPeriod = summary.Provider.CancelAtEndOfPeriod,
        DelayedCancelAt = summary.Provider.DelayedCancelAt,
        CanceledAt = summary.Provider.CanceledAt
    };
}
