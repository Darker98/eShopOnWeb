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

public class PlansModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;

    public PlansModel(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public IReadOnlyList<BillingPlan> Plans { get; set; } = new List<BillingPlan>();

    [BindProperty]
    public SubscribeInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Plans = await _subscriptionService.ListPlansAsync(cancellationToken);
        }
        catch (BillingProviderException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task<IActionResult> OnPostSubscribeAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Plans = await _subscriptionService.ListPlansAsync(cancellationToken);
            return Page();
        }

        Guard.Against.Null(User?.Identity?.Name, nameof(User.Identity.Name));
        var buyerId = User.Identity.Name;

        try
        {
            await _subscriptionService.SubscribeAsync(buyerId, buyerId, Input.FirstName, Input.LastName, Input.ProductHandle, paymentToken: null, cancellationToken);
        }
        catch (InvalidSubscriptionStateException ex)
        {
            ErrorMessage = ex.Message;
            Plans = await _subscriptionService.ListPlansAsync(cancellationToken);
            return Page();
        }
        catch (BillingProviderException ex)
        {
            ErrorMessage = ex.Message;
            Plans = await _subscriptionService.ListPlansAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage("/Subscriptions/Mine");
    }

    public class SubscribeInputModel
    {
        [Required]
        public string ProductHandle { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;
    }
}
