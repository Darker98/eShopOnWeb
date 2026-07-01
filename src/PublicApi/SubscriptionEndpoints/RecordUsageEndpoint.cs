using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BlazorShared.Authorization;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class RecordUsageRequest : BaseRequest
{
    /// <summary>Bound manually from the route in HandleAsync, not via [FromRoute] - see the comment there.</summary>
    public int SubscriptionId { get; set; }

    [Required]
    [Range(typeof(decimal), "-1000000", "1000000")]
    public decimal Quantity { get; set; }

    [StringLength(500)]
    public string? Memo { get; set; }
}

public class RecordUsageResponse : BaseResponse
{
    public RecordUsageResponse(System.Guid correlationId) : base(correlationId) { }
    public RecordUsageResponse() { }

    public long ProviderUsageId { get; set; }
    public decimal QuantityRecorded { get; set; }
    public int PeriodToDateUnitBalance { get; set; }
}

/// <summary>UC2: report metered usage on a subscription. Own subscription for any authenticated user; admin role required for any other subscription.</summary>
public class RecordUsageEndpoint : EndpointBaseAsync
    .WithRequest<RecordUsageRequest>
    .WithActionResult<RecordUsageResponse>
{
    private readonly ISubscriptionService _subscriptionService;

    public RecordUsageEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost("api/subscriptions/{subscriptionId}/usage")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerOperation(
        Summary = "Record metered usage",
        Description = "Records a unit (or batch) of api-call usage against the subscription's metered component, idempotently on the Idempotency-Key header.",
        OperationId = "subscriptions.recordUsage",
        Tags = new[] { "SubscriptionEndpoints" })]
    public override async Task<ActionResult<RecordUsageResponse>> HandleAsync(RecordUsageRequest request, CancellationToken cancellationToken = default)
    {
        // subscriptionId and the Idempotency-Key header are bound manually rather than via [FromRoute]/
        // [FromHeader] properties on the request DTO: any binding-source attribute on a sibling property
        // of the same complex type suppresses ASP.NET Core's otherwise-automatic body inference for the
        // remaining properties (Quantity/Memo), which would then silently bind from query/form instead
        // of the JSON body.
        request.SubscriptionId = int.Parse(RouteData.Values["subscriptionId"]!.ToString()!);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyHeader) || string.IsNullOrEmpty(idempotencyKeyHeader))
        {
            ModelState.AddModelError("Idempotency-Key", "The Idempotency-Key header is required.");
            return BadRequest(ModelState);
        }

        var actorBuyerId = User.Identity?.Name;
        if (string.IsNullOrEmpty(actorBuyerId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole(Constants.Roles.ADMINISTRATORS);
        var response = new RecordUsageResponse(request.CorrelationId());

        var usage = await _subscriptionService.RecordUsageAsync(actorBuyerId, isAdmin, request.SubscriptionId, request.Quantity, request.Memo, idempotencyKeyHeader.ToString(), cancellationToken);
        response.ProviderUsageId = usage.ProviderUsageId;
        response.QuantityRecorded = usage.QuantityRecorded;
        response.PeriodToDateUnitBalance = usage.PeriodToDateUnitBalance;

        return Ok(response);
    }
}
