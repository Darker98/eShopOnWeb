using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class SubscriptionsByUserSpecification : Specification<Subscription>
{
    public SubscriptionsByUserSpecification(string buyerId)
    {
        Query.Where(s => s.BuyerId == buyerId);
    }
}
