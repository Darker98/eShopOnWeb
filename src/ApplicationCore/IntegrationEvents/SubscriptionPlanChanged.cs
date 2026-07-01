using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents;

/// <summary>Published in-process, best-effort, after a plan-change (UC3) has been committed.</summary>
public class SubscriptionPlanChanged : INotification
{
    public SubscriptionPlanChanged(string buyerId, int subscriptionId, int providerSubscriptionId, string oldProductHandle, string newProductHandle, PlanChangeTiming timing)
    {
        BuyerId = buyerId;
        SubscriptionId = subscriptionId;
        ProviderSubscriptionId = providerSubscriptionId;
        OldProductHandle = oldProductHandle;
        NewProductHandle = newProductHandle;
        Timing = timing;
    }

    public string BuyerId { get; }
    public int SubscriptionId { get; }
    public int ProviderSubscriptionId { get; }
    public string OldProductHandle { get; }
    public string NewProductHandle { get; }
    public PlanChangeTiming Timing { get; }
}
