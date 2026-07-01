using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class SubscribeRequest : BaseRequest
{
    [Required]
    public string ProductHandle { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Chargify.js token for plans where the selected plan requires a payment method. Optional otherwise.</summary>
    public string? PaymentToken { get; set; }
}

public class SubscribeResponse : BaseResponse
{
    public SubscribeResponse(System.Guid correlationId) : base(correlationId) { }
    public SubscribeResponse() { }

    public SubscriptionDto Subscription { get; set; } = new();
}

/// <summary>UC1: enroll the authenticated customer in a plan. Mirrors CreateCatalogItemEndpoint's admin variant, minus the role restriction - any authenticated customer may subscribe themselves.</summary>
public class SubscribeEndpoint : EndpointBaseAsync
    .WithRequest<SubscribeRequest>
    .WithActionResult<SubscribeResponse>
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscribeEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost("api/subscriptions")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerOperation(
        Summary = "Subscribe to a plan",
        Description = "Ensures a billing-provider customer exists for the authenticated user and enrolls it in the chosen plan.",
        OperationId = "subscriptions.subscribe",
        Tags = new[] { "SubscriptionEndpoints" })]
    public override async Task<ActionResult<SubscribeResponse>> HandleAsync(SubscribeRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var buyerId = User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Unauthorized();
        }

        var response = new SubscribeResponse(request.CorrelationId());
        var summary = await _subscriptionService.SubscribeAsync(buyerId, buyerId, request.FirstName, request.LastName, request.ProductHandle, request.PaymentToken, cancellationToken);
        response.Subscription = SubscriptionDto.FromSummary(summary);

        return Ok(response);
    }
}
