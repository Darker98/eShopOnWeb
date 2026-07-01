using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>Published in-process, best-effort, after a lifecycle transition (UC4: pause/resume/cancel/reactivate).</summary>
public class SubscriptionStateChanged : INotification
{
    public SubscriptionStateChanged(string buyerId, int subscriptionId, int providerSubscriptionId, string oldState, string newState)
    {
        BuyerId = buyerId;
        SubscriptionId = subscriptionId;
        ProviderSubscriptionId = providerSubscriptionId;
        OldState = oldState;
        NewState = newState;
    }

    public string BuyerId { get; }
    public int SubscriptionId { get; }
    public int ProviderSubscriptionId { get; }
    public string OldState { get; }
    public string NewState { get; }
}
