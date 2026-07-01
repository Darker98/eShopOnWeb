using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>Published in-process, best-effort, after a subscription is successfully created in the billing provider (UC1).</summary>
public class SubscriptionActivated : INotification
{
    public SubscriptionActivated(string buyerId, int subscriptionId, int providerSubscriptionId, string productHandle)
    {
        BuyerId = buyerId;
        SubscriptionId = subscriptionId;
        ProviderSubscriptionId = providerSubscriptionId;
        ProductHandle = productHandle;
    }

    public string BuyerId { get; }
    public int SubscriptionId { get; }
    public int ProviderSubscriptionId { get; }
    public string ProductHandle { get; }
}
