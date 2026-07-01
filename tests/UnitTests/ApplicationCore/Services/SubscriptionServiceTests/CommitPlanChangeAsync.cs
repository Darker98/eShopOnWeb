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

public class CommitPlanChangeAsync
{
    private const string BuyerId = "buyer@example.com";
    private readonly IRepository<Subscription> _subscriptionRepo = Substitute.For<IRepository<Subscription>>();
    private readonly IRepository<UsageRecord> _usageRepo = Substitute.For<IRepository<UsageRecord>>();
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildService() => new(_subscriptionRepo, _usageRepo, _billingClient, _publisher, _logger);

    [Fact]
    public async Task RejectsAsStaleWhenFreshPreviewDisagreesWithExpectedAmount()
    {
        var subscription = new Subscription(BuyerId, 555, 9001, "eshop-pro", "active");
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);
        _billingClient.PreviewPlanChangeNowAsync(9001, "basic-plan", Arg.Any<CancellationToken>())
            .Returns(new BillingProrationPreview(500, 2900, 500, 0));

        var service = BuildService();

        await Assert.ThrowsAsync<StalePlanChangePreviewException>(() =>
            service.CommitPlanChangeAsync(BuyerId, isAdmin: false, 1, "basic-plan", PlanChangeTiming.Now, expectedProratedAdjustmentInCents: 999, CancellationToken.None));

        await _billingClient.DidNotReceive().CommitPlanChangeNowAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitsWhenFreshPreviewMatchesExpectedAmount()
    {
        var subscription = new Subscription(BuyerId, 555, 9001, "eshop-pro", "active");
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);
        _billingClient.PreviewPlanChangeNowAsync(9001, "basic-plan", Arg.Any<CancellationToken>())
            .Returns(new BillingProrationPreview(500, 2900, 500, 0));
        _billingClient.CommitPlanChangeNowAsync(9001, "basic-plan", Arg.Any<CancellationToken>())
            .Returns(new BillingSubscription(9001, 555, "basic-plan", "active", 0, null, null, null, null, null));

        var service = BuildService();
        var result = await service.CommitPlanChangeAsync(BuyerId, isAdmin: false, 1, "basic-plan", PlanChangeTiming.Now, expectedProratedAdjustmentInCents: 500, CancellationToken.None);

        Assert.Equal("basic-plan", result.NewProductHandle);
        await _billingClient.Received(1).CommitPlanChangeNowAsync(9001, "basic-plan", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectsChangingToTheSamePlan()
    {
        var subscription = new Subscription(BuyerId, 555, 9001, "eshop-pro", "active");
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);

        var service = BuildService();

        await Assert.ThrowsAsync<InvalidSubscriptionStateException>(() =>
            service.CommitPlanChangeAsync(BuyerId, isAdmin: false, 1, "eshop-pro", PlanChangeTiming.Now, null, CancellationToken.None));
    }

    [Fact]
    public async Task AtRenewalScheduleDoesNotCallMigrationsEndpoint()
    {
        var subscription = new Subscription(BuyerId, 555, 9001, "eshop-pro", "active");
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);
        _billingClient.SchedulePlanChangeAtRenewalAsync(9001, "basic-plan", Arg.Any<CancellationToken>())
            .Returns(new BillingSubscription(9001, 555, "eshop-pro", "active", 0, null, null, null, null, null));

        var service = BuildService();
        await service.CommitPlanChangeAsync(BuyerId, isAdmin: false, 1, "basic-plan", PlanChangeTiming.AtRenewal, null, CancellationToken.None);

        await _billingClient.DidNotReceive().PreviewPlanChangeNowAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _billingClient.DidNotReceive().CommitPlanChangeNowAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
