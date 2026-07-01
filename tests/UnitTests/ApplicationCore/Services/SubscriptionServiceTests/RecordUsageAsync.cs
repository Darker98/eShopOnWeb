using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SubscriptionServiceTests;

public class RecordUsageAsync
{
    private const string BuyerId = "buyer@example.com";
    private readonly IRepository<Subscription> _subscriptionRepo = Substitute.For<IRepository<Subscription>>();
    private readonly IRepository<UsageRecord> _usageRepo = Substitute.For<IRepository<UsageRecord>>();
    private readonly IBillingClient _billingClient = Substitute.For<IBillingClient>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IAppLogger<SubscriptionService> _logger = Substitute.For<IAppLogger<SubscriptionService>>();

    private SubscriptionService BuildService() => new(_subscriptionRepo, _usageRepo, _billingClient, _publisher, _logger);

    private static Subscription BuildOwnedSubscription() => new(BuyerId, 555, 9001, "eshop-pro", "active");

    [Fact]
    public async Task RecordsUsageAndPersistsALedgerEntry()
    {
        var subscription = BuildOwnedSubscription();
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);
        _usageRepo.FirstOrDefaultAsync(Arg.Any<UsageRecordByIdempotencyKeySpecification>(), Arg.Any<CancellationToken>()).Returns((UsageRecord?)null);
        _billingClient.RecordUsageAsync(9001, 5m, "batch", Arg.Any<CancellationToken>()).Returns(new BillingUsageResult(777, 5m, "batch"));
        _billingClient.GetUsageBalanceAsync(9001, Arg.Any<CancellationToken>()).Returns(42);

        var service = BuildService();
        var result = await service.RecordUsageAsync(BuyerId, isAdmin: false, 1, 5m, "batch", "key-1", CancellationToken.None);

        Assert.Equal(777, result.ProviderUsageId);
        Assert.Equal(42, result.PeriodToDateUnitBalance);
        await _usageRepo.Received(1).AddAsync(Arg.Is<UsageRecord>(u => u.IdempotencyKey == "key-1" && u.ProviderSubscriptionId == 9001), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateIdempotencyKeyReplaysWithoutCallingProviderAgain()
    {
        var subscription = BuildOwnedSubscription();
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);
        var existing = new UsageRecord(9001, "key-1", 5m, "batch", 777);
        _usageRepo.FirstOrDefaultAsync(Arg.Any<UsageRecordByIdempotencyKeySpecification>(), Arg.Any<CancellationToken>()).Returns(existing);
        _billingClient.GetUsageBalanceAsync(9001, Arg.Any<CancellationToken>()).Returns(42);

        var service = BuildService();
        var result = await service.RecordUsageAsync(BuyerId, isAdmin: false, 1, 5m, "batch", "key-1", CancellationToken.None);

        Assert.Equal(777, result.ProviderUsageId);
        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<int>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NonOwnerNonAdminCannotRecordUsageForSomeoneElsesSubscription()
    {
        var subscription = BuildOwnedSubscription();
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);

        var service = BuildService();

        await Assert.ThrowsAsync<SubscriptionNotFoundException>(() =>
            service.RecordUsageAsync("someone-else@example.com", isAdmin: false, 1, 5m, null, "key-1", CancellationToken.None));

        await _billingClient.DidNotReceive().RecordUsageAsync(Arg.Any<int>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminCanRecordUsageForAnySubscription()
    {
        var subscription = BuildOwnedSubscription();
        _subscriptionRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(subscription);
        _usageRepo.FirstOrDefaultAsync(Arg.Any<UsageRecordByIdempotencyKeySpecification>(), Arg.Any<CancellationToken>()).Returns((UsageRecord?)null);
        _billingClient.RecordUsageAsync(9001, 1m, null, Arg.Any<CancellationToken>()).Returns(new BillingUsageResult(1, 1m, null));
        _billingClient.GetUsageBalanceAsync(9001, Arg.Any<CancellationToken>()).Returns(1);

        var service = BuildService();
        var result = await service.RecordUsageAsync("admin@example.com", isAdmin: true, 1, 1m, null, "key-2", CancellationToken.None);

        Assert.Equal(1, result.PeriodToDateUnitBalance);
    }
}
