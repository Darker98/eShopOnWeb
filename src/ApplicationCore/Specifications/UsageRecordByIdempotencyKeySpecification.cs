using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class UsageRecordByIdempotencyKeySpecification : Specification<UsageRecord>
{
    public UsageRecordByIdempotencyKeySpecification(int providerSubscriptionId, string idempotencyKey)
    {
        Query.Where(u => u.ProviderSubscriptionId == providerSubscriptionId
            && u.IdempotencyKey == idempotencyKey);
    }
}
