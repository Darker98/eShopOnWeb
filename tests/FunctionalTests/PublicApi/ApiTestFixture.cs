using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.PublicApi.AuthEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.eShopWeb.FunctionalTests.PublicApi;

public class TestApiApplication : WebApplicationFactory<AuthenticateEndpoint>
{
    private readonly string _environment = "Testing";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        // Add mock/test services to the builder here
        builder.ConfigureServices(services =>
        {
            services.AddScoped(sp =>
            {
                // Replace SQLite with in-memory database for tests
                return new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase("DbForPublicApi")
                .UseApplicationServiceProvider(sp)
                .Options;
            });
            services.AddScoped(sp =>
            {
                // Replace SQLite with in-memory database for tests
                return new DbContextOptionsBuilder<AppIdentityDbContext>()
                .UseInMemoryDatabase("IdentityDbForPublicApi")
                .UseApplicationServiceProvider(sp)
                .Options;
            });

            // No test should make a real outbound call to Maxio (quality-gate.md J5); dummy values
            // satisfy MaxioSettings' [Required] DataAnnotations so ValidateOnStart doesn't fail the host.
            services.PostConfigure<Microsoft.eShopWeb.Infrastructure.Configuration.MaxioSettings>(s =>
            {
                s.ApiKey = "test-key";
                s.Subdomain = "test-subdomain";
                s.ProductFamilyHandle = "test-family";
                s.DefaultProductHandle = "test-product";
                s.MeteredComponentHandle = "test-component";
                s.SkipStartupValidation = true;
            });
        });

        return base.CreateHost(builder);
    }
}
