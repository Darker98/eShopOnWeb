using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>
/// eShopOnWeb's durable cache of the userId &lt;-&gt; Maxio customer/subscription mapping.
/// Maxio remains the source of truth for billing state; this entity lets "my subscriptions"
/// resolve without depending on Maxio's list-by-reference call on every request.
/// </summary>
public class Subscription : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Subscription() { }
#pragma warning restore CS8618

    public Subscription(string buyerId, int providerCustomerId, int providerSubscriptionId, string productHandle, string state)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(providerCustomerId, nameof(providerCustomerId));
        Guard.Against.NegativeOrZero(providerSubscriptionId, nameof(providerSubscriptionId));
        Guard.Against.NullOrEmpty(productHandle, nameof(productHandle));
        Guard.Against.NullOrEmpty(state, nameof(state));

        BuyerId = buyerId;
        ProviderCustomerId = providerCustomerId;
        ProviderSubscriptionId = providerSubscriptionId;
        ProductHandle = productHandle;
        State = state;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>The eShopOnWeb Identity user reference (User.Identity.Name), mirroring Order.BuyerId.</summary>
    public string BuyerId { get; private set; }

    /// <summary>The Maxio customer id ("customer" resource) that owns this subscription.</summary>
    public int ProviderCustomerId { get; private set; }

    /// <summary>The Maxio subscription id.</summary>
    public int ProviderSubscriptionId { get; private set; }

    /// <summary>The handle of the Maxio product (plan) currently assigned to the subscription.</summary>
    public string ProductHandle { get; private set; }

    /// <summary>The last known Maxio subscription state, synced after every provider call.</summary>
    public string State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void SyncFromProvider(string productHandle, string state)
    {
        Guard.Against.NullOrEmpty(productHandle, nameof(productHandle));
        Guard.Against.NullOrEmpty(state, nameof(state));

        ProductHandle = productHandle;
        State = state;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
