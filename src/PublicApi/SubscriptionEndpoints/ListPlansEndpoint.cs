using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class ListPlansRequest : BaseRequest
{
}

public class ListPlansResponse : BaseResponse
{
    public ListPlansResponse(System.Guid correlationId) : base(correlationId) { }
    public ListPlansResponse() { }

    public List<PlanDto> Plans { get; set; } = new();
}

public class PlanDto
{
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PriceInCents { get; set; }
    public int IntervalCount { get; set; }
    public string IntervalUnit { get; set; } = string.Empty;
    public bool RequiresPaymentMethod { get; set; }
}

/// <summary>UC1 step 1: browse the catalog of available plans. Anonymous - mirrors CatalogItemListPagedEndpoint.</summary>
public class ListPlansEndpoint : EndpointBaseAsync
    .WithRequest<ListPlansRequest>
    .WithActionResult<ListPlansResponse>
{
    private readonly ISubscriptionService _subscriptionService;

    public ListPlansEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("api/subscriptions/plans")]
    [SwaggerOperation(
        Summary = "List available subscription plans",
        Description = "Returns the sandbox's available recurring plans with price and billing interval.",
        OperationId = "subscriptions.listPlans",
        Tags = new[] { "SubscriptionEndpoints" })]
    public override async Task<ActionResult<ListPlansResponse>> HandleAsync([FromQuery] ListPlansRequest request, CancellationToken cancellationToken = default)
    {
        var response = new ListPlansResponse(request.CorrelationId());

        var plans = await _subscriptionService.ListPlansAsync(cancellationToken);
        response.Plans = plans.Select(p => new PlanDto
        {
            Handle = p.Handle,
            Name = p.Name,
            PriceInCents = p.PriceInCents,
            IntervalCount = p.IntervalCount,
            IntervalUnit = p.IntervalUnit,
            RequiresPaymentMethod = p.RequiresPaymentMethod
        }).ToList();

        return Ok(response);
    }
}
