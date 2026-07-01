using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LifecycleAction
{
    Pause,
    Resume,
    Cancel,
    Reactivate
}

public class LifecycleRequest : BaseRequest
{
    /// <summary>Bound manually from the route in HandleAsync - see RecordUsageEndpoint's HandleAsync for why.</summary>
    public int SubscriptionId { get; set; }

    [Required]
    public LifecycleAction Action { get; set; }

    /// <summary>Only meaningful when Action is Cancel.</summary>
    public CancelTiming CancelTiming { get; set; } = CancelTiming.Immediate;

    [StringLength(500)]
    public string? Reason { get; set; }
}

public class LifecycleResponse : BaseResponse
{
    public LifecycleResponse(System.Guid correlationId) : base(correlationId) { }
    public LifecycleResponse() { }

    public SubscriptionDto Subscription { get; set; } = new();
}

/// <summary>UC4: one management surface for pause / resume / cancel (immediate or end-of-period) / reactivate.</summary>
public class LifecycleEndpoint : EndpointBaseAsync
    .WithRequest<LifecycleRequest>
    .WithActionResult<LifecycleResponse>
{
    private readonly ISubscriptionService _subscriptionService;

    public LifecycleEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost("api/subscriptions/{subscriptionId}/lifecycle")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerOperation(
        Summary = "Apply a lifecycle transition",
        Description = "Pause, resume, cancel (immediate or end-of-period), or reactivate a subscription.",
        OperationId = "subscriptions.lifecycle",
        Tags = new[] { "SubscriptionEndpoints" })]
    public override async Task<ActionResult<LifecycleResponse>> HandleAsync(LifecycleRequest request, CancellationToken cancellationToken = default)
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

        var summary = request.Action switch
        {
            LifecycleAction.Pause => await _subscriptionService.PauseAsync(actorBuyerId, isAdmin, request.SubscriptionId, cancellationToken),
            LifecycleAction.Resume => await _subscriptionService.ResumeAsync(actorBuyerId, isAdmin, request.SubscriptionId, cancellationToken),
            LifecycleAction.Cancel => await _subscriptionService.CancelAsync(actorBuyerId, isAdmin, request.SubscriptionId, request.CancelTiming, request.Reason, cancellationToken),
            LifecycleAction.Reactivate => await _subscriptionService.ReactivateAsync(actorBuyerId, isAdmin, request.SubscriptionId, cancellationToken),
            _ => throw new System.ArgumentOutOfRangeException(nameof(request.Action), request.Action, "Unsupported lifecycle action.")
        };

        var response = new LifecycleResponse(request.CorrelationId())
        {
            Subscription = SubscriptionDto.FromSummary(summary)
        };

        return Ok(response);
    }
}
