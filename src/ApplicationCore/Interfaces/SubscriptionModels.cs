namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>An eShopOnWeb subscription paired with its current snapshot from the billing provider.</summary>
public record SubscriptionSummary(int SubscriptionId, BillingSubscription Provider);

public record UsageSummary(long ProviderUsageId, decimal QuantityRecorded, int PeriodToDateUnitBalance);

public record PlanChangeResult(SubscriptionSummary Subscription, string OldProductHandle, string NewProductHandle, PlanChangeTiming Timing, BillingProrationPreview? Proration);
