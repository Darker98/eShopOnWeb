using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;

namespace PublicApiIntegrationTests;

[TestClass]
public class ProgramTest
{
    private static WebApplicationFactory<Program> _application = new();

    public static HttpClient NewClient
    {
        get
        {
            return _application.CreateClient();
        }
    }

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _)
    {
        _application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                // No test should make a real outbound call to Maxio at startup (quality-gate.md J5).
                services.PostConfigure<MaxioSettings>(s => s.SkipStartupValidation = true)));
    }
}
