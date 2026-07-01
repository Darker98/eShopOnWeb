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

public class PlanChangeRequest : BaseRequest
{
    /// <summary>Bound manually from the route in HandleAsync - see RecordUsageEndpoint's HandleAsync for why.</summary>
    public int SubscriptionId { get; set; }

    [Required]
    public string TargetProductHandle { get; set; } = string.Empty;

    [Required]
    public PlanChangeTiming Timing { get; set; }

    /// <summary>The prorated_adjustment_in_cents shown to the customer by the preview call. When set and Timing is Now, a mismatch against a freshly-computed preview rejects the commit as a stale preview (AC-07b).</summary>
    public int? ExpectedProratedAdjustmentInCents { get; set; }
}

public class PlanChangeResponse : BaseResponse
{
    public PlanChangeResponse(System.Guid correlationId) : base(correlationId) { }
    public PlanChangeResponse() { }

    public SubscriptionDto Subscription { get; set; } = new();
    public string OldProductHandle { get; set; } = string.Empty;
    public string NewProductHandle { get; set; } = string.Empty;
    public int? ProratedAdjustmentInCents { get; set; }
}

/// <summary>UC3 step 3-4: commit a previously-previewed plan change.</summary>
public class PlanChangeEndpoint : EndpointBaseAsync
    .WithRequest<PlanChangeRequest>
    .WithActionResult<PlanChangeResponse>
{
    private readonly ISubscriptionService _subscriptionService;

    public PlanChangeEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost("api/subscriptions/{subscriptionId}/plan-change")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerOperation(
        Summary = "Commit a plan change",
        OperationId = "subscriptions.commitPlanChange",
        Tags = new[] { "SubscriptionEndpoints" })]
    public override async Task<ActionResult<PlanChangeResponse>> HandleAsync(PlanChangeRequest request, CancellationToken cancellationToken = default)
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
        var result = await _subscriptionService.CommitPlanChangeAsync(actorBuyerId, isAdmin, request.SubscriptionId, request.TargetProductHandle, request.Timing, request.ExpectedProratedAdjustmentInCents, cancellationToken);

        var response = new PlanChangeResponse(request.CorrelationId())
        {
            Subscription = SubscriptionDto.FromSummary(result.Subscription),
            OldProductHandle = result.OldProductHandle,
            NewProductHandle = result.NewProductHandle,
            ProratedAdjustmentInCents = result.Proration?.ProratedAdjustmentInCents
        };

        return Ok(response);
    }
}
