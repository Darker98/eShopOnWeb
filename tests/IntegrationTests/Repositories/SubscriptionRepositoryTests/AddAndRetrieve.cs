using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Data;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Repositories.SubscriptionRepositoryTests;

public class AddAndRetrieve
{
    private readonly CatalogContext _catalogContext;
    private readonly EfRepository<Subscription> _subscriptionRepository;

    public AddAndRetrieve()
    {
        var dbOptions = new DbContextOptionsBuilder<CatalogContext>()
            .UseInMemoryDatabase(databaseName: "TestSubscriptions")
            .Options;
        _catalogContext = new CatalogContext(dbOptions);
        _subscriptionRepository = new EfRepository<Subscription>(_catalogContext);
    }

    [Fact]
    public async Task PersistsAndReloadsTheUserProviderMapping()
    {
        var subscription = new Subscription("buyer-1@example.com", 555, 9001, "eshop-pro", "active");

        await _subscriptionRepository.AddAsync(subscription);

        var reloaded = await _subscriptionRepository.GetByIdAsync(subscription.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(9001, reloaded!.ProviderSubscriptionId);
        Assert.Equal("active", reloaded.State);
    }

    [Fact]
    public async Task ListsOnlyTheSpecifiedUsersSubscriptions()
    {
        await _subscriptionRepository.AddAsync(new Subscription("buyer-2@example.com", 1, 100, "eshop-pro", "active"));
        await _subscriptionRepository.AddAsync(new Subscription("buyer-2@example.com", 1, 101, "basic-plan", "canceled"));
        await _subscriptionRepository.AddAsync(new Subscription("someone-else@example.com", 2, 200, "eshop-pro", "active"));

        var mine = await _subscriptionRepository.ListAsync(new SubscriptionsByUserSpecification("buyer-2@example.com"));

        Assert.Equal(2, mine.Count);
        Assert.All(mine, s => Assert.Equal("buyer-2@example.com", s.BuyerId));
    }

    [Fact]
    public async Task SyncFromProviderUpdatesStateAndPersists()
    {
        var subscription = new Subscription("buyer-3@example.com", 1, 300, "eshop-pro", "active");
        await _subscriptionRepository.AddAsync(subscription);

        subscription.SyncFromProvider("eshop-pro", "on_hold");
        await _subscriptionRepository.UpdateAsync(subscription);

        var reloaded = await _subscriptionRepository.GetByIdAsync(subscription.Id);
        Assert.Equal("on_hold", reloaded!.State);
    }
}
