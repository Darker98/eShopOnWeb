using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.SubscriptionEndpoints;

/// <summary>
/// Every subscription route requires auth (plan.md AC-13); these calls never reach IBillingClient
/// - either [Authorize] rejects the request first, or (for a subscription id that does not exist in
/// the test's in-memory database) SubscriptionService's ownership check throws before any provider
/// call is made - so no test here makes a real outbound call to Maxio (api-integration-quality-gate.md Gate 10).
/// </summary>
[TestClass]
public class AuthorizationTests
{
    [TestMethod]
    public async Task SubscribeWithoutTokenReturnsUnauthorized()
    {
        var client = ProgramTest.NewClient;
        var response = await client.PostAsync("api/subscriptions", JsonBody(new { ProductHandle = "eshop-pro", FirstName = "A", LastName = "B" }));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task MySubscriptionsWithoutTokenReturnsUnauthorized()
    {
        var client = ProgramTest.NewClient;
        var response = await client.GetAsync("api/subscriptions/mine");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RecordUsageWithoutTokenReturnsUnauthorized()
    {
        var client = ProgramTest.NewClient;
        var request = new HttpRequestMessage(HttpMethod.Post, "api/subscriptions/1/usage")
        {
            Content = JsonBody(new { Quantity = 1 })
        };
        request.Headers.Add("Idempotency-Key", "test-key");

        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task LifecycleWithoutTokenReturnsUnauthorized()
    {
        var client = ProgramTest.NewClient;
        var response = await client.PostAsync("api/subscriptions/1/lifecycle", JsonBody(new { Action = "Pause" }));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RecordUsageOnAnUnknownSubscriptionReturnsNotFoundForANonAdminUser()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var request = new HttpRequestMessage(HttpMethod.Post, "api/subscriptions/999999/usage")
        {
            Content = JsonBody(new { Quantity = 1 })
        };
        request.Headers.Add("Idempotency-Key", "test-key-999999");

        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task LifecycleOnAnUnknownSubscriptionReturnsNotFoundForAnAdminUser()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetAdminUserToken());

        var response = await client.PostAsync("api/subscriptions/999999/lifecycle", JsonBody(new { Action = "Pause" }));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static StringContent JsonBody(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
