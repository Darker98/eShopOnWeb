using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Data;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Repositories.UsageRecordRepositoryTests;

public class AddAndFindByIdempotencyKey
{
    private readonly CatalogContext _catalogContext;
    private readonly EfRepository<UsageRecord> _usageRepository;

    public AddAndFindByIdempotencyKey()
    {
        var dbOptions = new DbContextOptionsBuilder<CatalogContext>()
            .UseInMemoryDatabase(databaseName: "TestUsageRecords")
            .Options;
        _catalogContext = new CatalogContext(dbOptions);
        _usageRepository = new EfRepository<UsageRecord>(_catalogContext);
    }

    [Fact]
    public async Task FindsAPreviouslyRecordedUsageByItsIdempotencyKey()
    {
        await _usageRepository.AddAsync(new UsageRecord(9001, "key-abc", 5m, "batch", 777));

        var found = await _usageRepository.FirstOrDefaultAsync(new UsageRecordByIdempotencyKeySpecification(9001, "key-abc"));

        Assert.NotNull(found);
        Assert.Equal(777, found!.ProviderUsageId);
    }

    [Fact]
    public async Task DoesNotMatchTheSameKeyUnderADifferentSubscription()
    {
        await _usageRepository.AddAsync(new UsageRecord(9001, "key-abc", 5m, "batch", 777));

        var found = await _usageRepository.FirstOrDefaultAsync(new UsageRecordByIdempotencyKeySpecification(9002, "key-abc"));

        Assert.Null(found);
    }
}
