using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Web.Pages.Subscriptions;

public class MineModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;

    public MineModel(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public IReadOnlyList<SubscriptionSummary> Subscriptions { get; set; } = new List<SubscriptionSummary>();
    public IReadOnlyList<BillingPlan> Plans { get; set; } = new List<BillingPlan>();

    public string? ErrorMessage { get; set; }
    public string? StatusMessage { get; set; }

    /// <summary>Set only after a successful preview, so the confirm form can appear for that one subscription.</summary>
    public int? PreviewedSubscriptionId { get; set; }
    public BillingProrationPreview? Preview { get; set; }

    [BindProperty]
    public UsageInputModel Usage { get; set; } = new();

    [BindProperty]
    public LifecycleInputModel Lifecycle { get; set; } = new();

    [BindProperty]
    public PlanChangeInputModel PlanChange { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        Usage.IdempotencyKey = Guid.NewGuid().ToString();
    }

    public async Task<IActionResult> OnPostRecordUsageAsync(CancellationToken cancellationToken)
    {
        var buyerId = RequireBuyerId();

        try
        {
            var result = await _subscriptionService.RecordUsageAsync(buyerId, isAdmin: false, Usage.SubscriptionId, Usage.Quantity, Usage.Memo, Usage.IdempotencyKey, cancellationToken);
            StatusMessage = $"Recorded {result.QuantityRecorded} unit(s). Period-to-date balance: {result.PeriodToDateUnitBalance}.";
        }
        catch (Exception ex) when (ex is InvalidSubscriptionStateException or BillingProviderException or SubscriptionNotFoundException)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync(cancellationToken);
        Usage.IdempotencyKey = Guid.NewGuid().ToString();
        return Page();
    }

    public async Task<IActionResult> OnPostLifecycleAsync(CancellationToken cancellationToken)
    {
        var buyerId = RequireBuyerId();

        try
        {
            switch (Lifecycle.Action)
            {
                case "Pause":
                    await _subscriptionService.PauseAsync(buyerId, isAdmin: false, Lifecycle.SubscriptionId, cancellationToken);
                    break;
                case "Resume":
                    await _subscriptionService.ResumeAsync(buyerId, isAdmin: false, Lifecycle.SubscriptionId, cancellationToken);
                    break;
                case "CancelImmediate":
                    await _subscriptionService.CancelAsync(buyerId, isAdmin: false, Lifecycle.SubscriptionId, CancelTiming.Immediate, Lifecycle.Reason, cancellationToken);
                    break;
                case "CancelEndOfPeriod":
                    await _subscriptionService.CancelAsync(buyerId, isAdmin: false, Lifecycle.SubscriptionId, CancelTiming.EndOfPeriod, Lifecycle.Reason, cancellationToken);
                    break;
                case "Reactivate":
                    await _subscriptionService.ReactivateAsync(buyerId, isAdmin: false, Lifecycle.SubscriptionId, cancellationToken);
                    break;
            }
            StatusMessage = "Subscription updated.";
        }
        catch (Exception ex) when (ex is InvalidSubscriptionStateException or BillingProviderException or SubscriptionNotFoundException)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync(cancellationToken);
        Usage.IdempotencyKey = Guid.NewGuid().ToString();
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewPlanChangeAsync(CancellationToken cancellationToken)
    {
        var buyerId = RequireBuyerId();

        try
        {
            Preview = await _subscriptionService.PreviewPlanChangeAsync(buyerId, isAdmin: false, PlanChange.SubscriptionId, PlanChange.TargetProductHandle, PlanChange.Timing, cancellationToken);
            PreviewedSubscriptionId = PlanChange.SubscriptionId;
        }
        catch (Exception ex) when (ex is InvalidSubscriptionStateException or BillingProviderException or SubscriptionNotFoundException)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync(cancellationToken);
        Usage.IdempotencyKey = Guid.NewGuid().ToString();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmPlanChangeAsync(CancellationToken cancellationToken)
    {
        var buyerId = RequireBuyerId();

        try
        {
            var result = await _subscriptionService.CommitPlanChangeAsync(buyerId, isAdmin: false, PlanChange.SubscriptionId, PlanChange.TargetProductHandle, PlanChange.Timing, PlanChange.ExpectedProratedAdjustmentInCents, cancellationToken);
            StatusMessage = $"Moved from {result.OldProductHandle} to {result.NewProductHandle}.";
        }
        catch (Exception ex) when (ex is InvalidSubscriptionStateException or BillingProviderException or SubscriptionNotFoundException or StalePlanChangePreviewException)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync(cancellationToken);
        Usage.IdempotencyKey = Guid.NewGuid().ToString();
        return Page();
    }

    private string RequireBuyerId()
    {
        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        return User.Identity.Name;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var buyerId = RequireBuyerId();

        try
        {
            Subscriptions = await _subscriptionService.GetSubscriptionsForUserAsync(buyerId, cancellationToken);
            Plans = await _subscriptionService.ListPlansAsync(cancellationToken);
        }
        catch (BillingProviderException ex)
        {
            // Preserve an error already set by the action that called LoadAsync (e.g. a failed lifecycle
            // transition) rather than overwriting it with this page-refresh failure.
            ErrorMessage ??= ex.Message;
        }
    }

    public class UsageInputModel
    {
        public int SubscriptionId { get; set; }

        [Required]
        public decimal Quantity { get; set; } = 1;

        public string? Memo { get; set; }

        public string IdempotencyKey { get; set; } = string.Empty;
    }

    public class LifecycleInputModel
    {
        public int SubscriptionId { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty;

        public string? Reason { get; set; }
    }

    public class PlanChangeInputModel
    {
        public int SubscriptionId { get; set; }

        [Required]
        public string TargetProductHandle { get; set; } = string.Empty;

        public PlanChangeTiming Timing { get; set; }

        public int? ExpectedProratedAdjustmentInCents { get; set; }
    }
}
