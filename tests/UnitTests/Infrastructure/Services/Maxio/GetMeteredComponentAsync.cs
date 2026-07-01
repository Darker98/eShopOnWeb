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

public class GetMeteredComponentAsync
{
    private static MaxioBillingClient BuildClient(FakeHttpMessageHandler handler)
    {
        var settings = new MaxioSettings
        {
            ApiKey = "test-key",
            Subdomain = "apimatic-hackathon",
            ProductFamilyHandle = "eshop-subscribe",
            DefaultProductHandle = "eshop-pro",
            MeteredComponentHandle = "api-call"
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new System.Uri("https://apimatic-hackathon.chargify.com") };
        return new MaxioBillingClient(httpClient, Options.Create(settings), NullLogger<MaxioBillingClient>.Instance);
    }

    [Fact]
    public async Task ReturnsInfoWhenComponentIsMetered()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """
            { "component": { "id": 3033795, "handle": "api-call", "name": "API Calls", "kind": "metered_component" } }
            """);
        var client = BuildClient(handler);

        var component = await client.GetMeteredComponentAsync(CancellationToken.None);

        Assert.True(component.IsMetered);
        Assert.Equal("api-call", component.Handle);
    }

    [Theory]
    [InlineData("quantity_based_component")]
    [InlineData("on_off_component")]
    public async Task RefusesWhenComponentIsNotMetered(string actualKind)
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, $$"""
            { "component": { "id": 3033795, "handle": "api-call", "name": "API Calls", "kind": "{{actualKind}}" } }
            """);
        var client = BuildClient(handler);

        await Assert.ThrowsAsync<MeteredComponentMisconfiguredException>(() => client.GetMeteredComponentAsync(CancellationToken.None));
    }
}
