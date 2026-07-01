using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class MySubscriptionsRequest : BaseRequest
{
}

public class MySubscriptionsResponse : BaseResponse
{
    public MySubscriptionsResponse(System.Guid correlationId) : base(correlationId) { }
    public MySubscriptionsResponse() { }

    public List<SubscriptionDto> Subscriptions { get; set; } = new();
}

/// <summary>AC-06: lists the authenticated user's subscriptions only.</summary>
public class MySubscriptionsEndpoint : EndpointBaseAsync
    .WithRequest<MySubscriptionsRequest>
    .WithActionResult<MySubscriptionsResponse>
{
    private readonly ISubscriptionService _subscriptionService;

    public MySubscriptionsEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("api/subscriptions/mine")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerOperation(
        Summary = "List my subscriptions",
        Description = "Lists the authenticated user's own subscriptions, refreshed from the billing provider.",
        OperationId = "subscriptions.mine",
        Tags = new[] { "SubscriptionEndpoints" })]
    public override async Task<ActionResult<MySubscriptionsResponse>> HandleAsync([FromQuery] MySubscriptionsRequest request, CancellationToken cancellationToken = default)
    {
        var buyerId = User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Unauthorized();
        }

        var response = new MySubscriptionsResponse(request.CorrelationId());
        var subscriptions = await _subscriptionService.GetSubscriptionsForUserAsync(buyerId, cancellationToken);
        response.Subscriptions = subscriptions.Select(SubscriptionDto.FromSummary).ToList();

        return Ok(response);
    }
}
