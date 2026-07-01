using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class PauseAsync
{
    private const string BuyerId = "buyer@example.com";
    private readonly IRepository<Subscription> _subscriptionRepo = Substitute.For<IRepository<Subscription>>();
    private readonly IRepository<UsageRecord> _usageRepo = Substitute.For<IRepository<UsageRecord>>();
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildService() => new(_subscriptionRepo, _usageRepo, _billingClient, _publisher, _logger);

    [Fact]
    public async Task PausesAnActiveSubscriptionAndPublishesStateChanged()
    {
        var subscription = new Subscription(BuyerId, 555, 9001, "eshop-pro", "active");
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);
        _billingClient.PauseSubscriptionAsync(9001, Arg.Any<CancellationToken>())
            .Returns(new BillingSubscription(9001, 555, "eshop-pro", "on_hold", 0, null, null, null, null, null));

        var service = BuildService();
        var result = await service.PauseAsync(BuyerId, isAdmin: false, 1, CancellationToken.None);

        Assert.Equal("on_hold", result.Provider.State);
        await _publisher.Received(1).Publish(Arg.Any<Microsoft.eShopWeb.ApplicationCore.IntegrationEvents.SubscriptionStateChanged>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectsPausingASubscriptionAlreadyOnHoldWithoutCallingProvider()
    {
        var subscription = new Subscription(BuyerId, 555, 9001, "eshop-pro", "on_hold");
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);

        var service = BuildService();

        await Assert.ThrowsAsync<InvalidSubscriptionStateException>(() => service.PauseAsync(BuyerId, isAdmin: false, 1, CancellationToken.None));

        await _billingClient.DidNotReceive().PauseSubscriptionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NonOwnerCannotPauseSomeoneElsesSubscription()
    {
        var subscription = new Subscription(BuyerId, 555, 9001, "eshop-pro", "active");
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);

        var service = BuildService();

        await Assert.ThrowsAsync<SubscriptionNotFoundException>(() =>
            service.PauseAsync("someone-else@example.com", isAdmin: false, 1, CancellationToken.None));
    }

    [Fact]
    public async Task UnknownSubscriptionIdThrowsNotFound()
    {
        _subscriptionRepo.GetByIdAsync(404, Arg.Any<CancellationToken>()).Returns((Subscription?)null);

        var service = BuildService();

        await Assert.ThrowsAsync<SubscriptionNotFoundException>(() => service.PauseAsync(BuyerId, isAdmin: false, 404, CancellationToken.None));
    }
}
