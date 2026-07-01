using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.eShopWeb.Infrastructure.Services.Maxio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Services.Maxio;

public class ListPlansAsync
{
    private static MaxioSettings BuildSettings() => new()
    {
        ApiKey = "test-key",
        Subdomain = "apimatic-hackathon",
        Environment = "US",
        ProductFamilyHandle = "eshop-subscribe",
        DefaultProductHandle = "eshop-pro",
        MeteredComponentHandle = "api-call"
    };

    private static MaxioBillingClient BuildClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new System.Uri("https://apimatic-hackathon.chargify.com") };
        return new MaxioBillingClient(httpClient, Options.Create(BuildSettings()), NullLogger<MaxioBillingClient>.Instance);
    }

    [Fact]
    public async Task MapsProductsToPlans()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """
            [
              { "product": { "id": 7111477, "name": "Pro Plan", "handle": "eshop-pro", "price_in_cents": 29900, "interval": 1, "interval_unit": "month", "require_credit_card": false } },
              { "product": { "id": 7111478, "name": "Basic Plan", "handle": "basic-plan", "price_in_cents": 2900, "interval": 1, "interval_unit": "month", "require_credit_card": false } }
            ]
            """);
        var client = BuildClient(handler);

        var plans = await client.ListPlansAsync(CancellationToken.None);

        Assert.Equal(2, plans.Count);
        Assert.Equal("eshop-pro", plans[0].Handle);
        Assert.Equal(29900, plans[0].PriceInCents);
        Assert.False(plans[0].RequiresPaymentMethod);
        Assert.Contains("product_families/handle%3Aeshop-subscribe/products.json", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task ThrowsBillingProviderExceptionWithParsedMessagesOn422()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.UnprocessableEntity, """{"errors": ["Product family: could not be found."]}""");
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<BillingProviderException>(() => client.ListPlansAsync(CancellationToken.None));

        Assert.Equal(422, ex.HttpStatusCode);
        Assert.Contains("Product family: could not be found.", ex.ProviderMessages);
    }

    [Fact]
    public async Task DoesNotLeakRawDetailsForServerErrors()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "<html>Internal Server Error - stack trace at Foo.Bar()</html>");
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<BillingProviderException>(() => client.ListPlansAsync(CancellationToken.None));

        Assert.DoesNotContain("stack trace", ex.Message);
        Assert.Equal(500, ex.HttpStatusCode);
    }
}
