using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>
/// Application-side idempotency ledger for UC2 usage reporting. Maxio's usages.json operation
/// declares no idempotency key (see api-integration-quality-gate.md Gate 3 / plan.md AC-12), so
/// duplicate-report protection has to be enforced here, before the provider is ever called.
/// </summary>
public class UsageRecord : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private UsageRecord() { }
#pragma warning restore CS8618

    public UsageRecord(int providerSubscriptionId, string idempotencyKey, decimal quantity, string? memo, long providerUsageId)
    {
        Guard.Against.NegativeOrZero(providerSubscriptionId, nameof(providerSubscriptionId));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        ProviderSubscriptionId = providerSubscriptionId;
        IdempotencyKey = idempotencyKey;
        Quantity = quantity;
        Memo = memo;
        ProviderUsageId = providerUsageId;
        RecordedAt = DateTimeOffset.UtcNow;
    }

    public int ProviderSubscriptionId { get; private set; }
    public string IdempotencyKey { get; private set; }
    public decimal Quantity { get; private set; }
    public string? Memo { get; private set; }

    // Usage-Response.yaml declares id as int64, unlike every other Maxio resource id (int32).
    public long ProviderUsageId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
}
