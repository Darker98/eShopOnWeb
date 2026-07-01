using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.IntegrationEvents.Handlers;

/// <summary>
/// Best-effort, in-process audit logging for subscription lifecycle notifications (see plan.md
/// section 2.5). A handler failure is caught by MediatR's publish loop's caller, not here; it must
/// never roll back the billing-provider action that already succeeded.
/// </summary>
public class SubscriptionActivatedLoggingHandler : INotificationHandler<SubscriptionActivated>
{
    private readonly IAppLogger<SubscriptionActivatedLoggingHandler> _logger;

    public SubscriptionActivatedLoggingHandler(IAppLogger<SubscriptionActivatedLoggingHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionActivated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Subscription {SubscriptionId} (provider id {ProviderSubscriptionId}) activated on plan {ProductHandle} for buyer {BuyerId}",
            notification.SubscriptionId, notification.ProviderSubscriptionId, notification.ProductHandle, notification.BuyerId);
        return Task.CompletedTask;
    }
}

public class SubscriptionPlanChangedLoggingHandler : INotificationHandler<SubscriptionPlanChanged>
{
    private readonly IAppLogger<SubscriptionPlanChangedLoggingHandler> _logger;

    public SubscriptionPlanChangedLoggingHandler(IAppLogger<SubscriptionPlanChangedLoggingHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionPlanChanged notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Subscription {SubscriptionId} (provider id {ProviderSubscriptionId}) moved from plan {OldProductHandle} to {NewProductHandle} ({Timing}) for buyer {BuyerId}",
            notification.SubscriptionId, notification.ProviderSubscriptionId, notification.OldProductHandle, notification.NewProductHandle, notification.Timing, notification.BuyerId);
        return Task.CompletedTask;
    }
}

public class SubscriptionStateChangedLoggingHandler : INotificationHandler<SubscriptionStateChanged>
{
    private readonly IAppLogger<SubscriptionStateChangedLoggingHandler> _logger;

    public SubscriptionStateChangedLoggingHandler(IAppLogger<SubscriptionStateChangedLoggingHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SubscriptionStateChanged notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Subscription {SubscriptionId} (provider id {ProviderSubscriptionId}) transitioned {OldState} -> {NewState} for buyer {BuyerId}",
            notification.SubscriptionId, notification.ProviderSubscriptionId, notification.OldState, notification.NewState, notification.BuyerId);
        return Task.CompletedTask;
    }
}
