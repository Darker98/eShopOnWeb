using System.Collections.Generic;
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

public class SubscribeAsync
{
    private const string BuyerId = "buyer@example.com";
    private readonly IRepository<Subscription> _subscriptionRepo = Substitute.For<IRepository<Subscription>>();
    private readonly IRepository<UsageRecord> _usageRepo = Substitute.For<IRepository<UsageRecord>>();
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildService() => new(_subscriptionRepo, _usageRepo, _billingClient, _publisher, _logger);

    private static readonly BillingPlan ProPlan = new("eshop-pro", 7111477, "Pro Plan", 29900, 1, "month", false);

    [Fact]
    public async Task EnsuresCustomerOnceThenCreatesSubscriptionAndPersistsIt()
    {
        _billingClient.ListPlansAsync(Arg.Any<CancellationToken>()).Returns(new List<BillingPlan> { ProPlan });
        _billingClient.EnsureCustomerAsync(BuyerId, BuyerId, "Ada", "Lovelace", Arg.Any<CancellationToken>()).Returns(555);
        var created = new BillingSubscription(9001, 555, "eshop-pro", "active", 0, null, null, null, null, null);
        _billingClient.CreateSubscriptionAsync(555, "eshop-pro", null, Arg.Any<CancellationToken>()).Returns(created);

        var service = BuildService();
        var summary = await service.SubscribeAsync(BuyerId, BuyerId, "Ada", "Lovelace", "eshop-pro", null, CancellationToken.None);

        await _billingClient.Received(1).EnsureCustomerAsync(BuyerId, BuyerId, "Ada", "Lovelace", Arg.Any<CancellationToken>());
        await _subscriptionRepo.Received(1).AddAsync(Arg.Is<Subscription>(s => s.ProviderSubscriptionId == 9001 && s.BuyerId == BuyerId), Arg.Any<CancellationToken>());
        Assert.Equal(9001, summary.Provider.ProviderSubscriptionId);
    }

    [Fact]
    public async Task PublishesSubscriptionActivatedNotification()
    {
        _billingClient.ListPlansAsync(Arg.Any<CancellationToken>()).Returns(new List<BillingPlan> { ProPlan });
        _billingClient.EnsureCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(555);
        _billingClient.CreateSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new BillingSubscription(9001, 555, "eshop-pro", "active", 0, null, null, null, null, null));

        var service = BuildService();
        await service.SubscribeAsync(BuyerId, BuyerId, "Ada", "Lovelace", "eshop-pro", null, CancellationToken.None);

        await _publisher.Received(1).Publish(Arg.Any<Microsoft.eShopWeb.ApplicationCore.IntegrationEvents.SubscriptionActivated>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectsSubscribeWhenPlanRequiresPaymentMethodButNoneSupplied()
    {
        var cardRequiredPlan = new BillingPlan("card-plan", 1, "Card Plan", 1000, 1, "month", true);
        _billingClient.ListPlansAsync(Arg.Any<CancellationToken>()).Returns(new List<BillingPlan> { cardRequiredPlan });

        var service = BuildService();

        await Assert.ThrowsAsync<InvalidSubscriptionStateException>(() =>
            service.SubscribeAsync(BuyerId, BuyerId, "Ada", "Lovelace", "card-plan", null, CancellationToken.None));

        await _billingClient.DidNotReceive().EnsureCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectsSubscribeToAnUnknownPlanHandle()
    {
        _billingClient.ListPlansAsync(Arg.Any<CancellationToken>()).Returns(new List<BillingPlan> { ProPlan });

        var service = BuildService();

        await Assert.ThrowsAsync<InvalidSubscriptionStateException>(() =>
            service.SubscribeAsync(BuyerId, BuyerId, "Ada", "Lovelace", "does-not-exist", null, CancellationToken.None));
    }
}
