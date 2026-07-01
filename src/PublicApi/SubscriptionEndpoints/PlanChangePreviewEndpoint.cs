using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using BlazorShared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class PlanChangePreviewRequest : BaseRequest
{
    /// <summary>Bound manually from the route in HandleAsync - see RecordUsageEndpoint's HandleAsync for why.</summary>
    public int SubscriptionId { get; set; }

    [Required]
    public string TargetProductHandle { get; set; } = string.Empty;

    [Required]
    public PlanChangeTiming Timing { get; set; }
}

public class PlanChangePreviewResponse : BaseResponse
{
    public PlanChangePreviewResponse(System.Guid correlationId) : base(correlationId) { }
    public PlanChangePreviewResponse() { }

    /// <summary>Null when Timing is AtRenewal - the delayed product change applies no proration.</summary>
    public int? ProratedAdjustmentInCents { get; set; }
    public int? ChargeInCents { get; set; }
    public int? PaymentDueInCents { get; set; }
    public int? CreditAppliedInCents { get; set; }
}

/// <summary>UC3 step 1-2: preview the prorated cost of a plan change before the customer commits (AC-07).</summary>
public class PlanChangePreviewEndpoint : EndpointBaseAsync
    .WithRequest<PlanChangePreviewRequest>
    .WithActionResult<PlanChangePreviewResponse>
{
    private readonly ISubscriptionService _subscriptionService;

    public PlanChangePreviewEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost("api/subscriptions/{subscriptionId}/plan-change/preview")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerOperation(
        Summary = "Preview a plan change",
        OperationId = "subscriptions.previewPlanChange",
        Tags = new[] { "SubscriptionEndpoints" })]
    public override async Task<ActionResult<PlanChangePreviewResponse>> HandleAsync(PlanChangePreviewRequest request, CancellationToken cancellationToken = default)
    {
        request.SubscriptionId = int.Parse(RouteData.Values["subscriptionId"]!.ToString()!);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorBuyerId = User.Identity?.Name;
        if (string.IsNullOrEmpty(actorBuyerId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole(Constants.Roles.ADMINISTRATORS);
        var preview = await _subscriptionService.PreviewPlanChangeAsync(actorBuyerId, isAdmin, request.SubscriptionId, request.TargetProductHandle, request.Timing, cancellationToken);

        var response = new PlanChangePreviewResponse(request.CorrelationId())
        {
            ProratedAdjustmentInCents = preview?.ProratedAdjustmentInCents,
            ChargeInCents = preview?.ChargeInCents,
            PaymentDueInCents = preview?.PaymentDueInCents,
            CreditAppliedInCents = preview?.CreditAppliedInCents
        };

        return Ok(response);
    }
}
