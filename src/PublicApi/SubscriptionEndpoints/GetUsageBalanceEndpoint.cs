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

public class GetUsageBalanceRequest : BaseRequest
{
    [FromRoute(Name = "subscriptionId")]
    public int SubscriptionId { get; set; }
}

public class GetUsageBalanceResponse : BaseResponse
{
    public GetUsageBalanceResponse(System.Guid correlationId) : base(correlationId) { }
    public GetUsageBalanceResponse() { }

    public int PeriodToDateUnitBalance { get; set; }
}

/// <summary>UC2: read the running period-to-date usage total without recording a new unit.</summary>
public class GetUsageBalanceEndpoint : EndpointBaseAsync
    .WithRequest<GetUsageBalanceRequest>
    .WithActionResult<GetUsageBalanceResponse>
{
    private readonly ISubscriptionService _subscriptionService;

    public GetUsageBalanceEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("api/subscriptions/{subscriptionId}/usage")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerOperation(
        Summary = "Read period-to-date usage balance",
        OperationId = "subscriptions.getUsageBalance",
        Tags = new[] { "SubscriptionEndpoints" })]
    public override async Task<ActionResult<GetUsageBalanceResponse>> HandleAsync([FromRoute] GetUsageBalanceRequest request, CancellationToken cancellationToken = default)
    {
        var actorBuyerId = User.Identity?.Name;
        if (string.IsNullOrEmpty(actorBuyerId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole(Constants.Roles.ADMINISTRATORS);
        var response = new GetUsageBalanceResponse(request.CorrelationId());
        response.PeriodToDateUnitBalance = await _subscriptionService.GetUsageBalanceAsync(actorBuyerId, isAdmin, request.SubscriptionId, cancellationToken);

        return Ok(response);
    }
}
