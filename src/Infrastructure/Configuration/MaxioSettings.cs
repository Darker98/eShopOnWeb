using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Configuration;

/// <summary>
/// Typed configuration for the Maxio Advanced Billing integration (mirrors CatalogSettings). Bound
/// from the "Maxio" section; ApiKey must come from user-secrets/environment, never appsettings.json
/// (plan.md section 6 / quality-gate.md C2).
/// </summary>
public class MaxioSettings
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>"US" or "EU" - selects which Maxio server template to use (see openapi.yaml x-server-configuration).</summary>
    public string Environment { get; set; } = "US";

    [Required]
    public string ProductFamilyHandle { get; set; } = string.Empty;

    public int ProductFamilyId { get; set; }

    [Required]
    public string DefaultProductHandle { get; set; } = string.Empty;

    public int DefaultProductId { get; set; }

    public string AlternateProductHandle { get; set; } = string.Empty;

    public int AlternateProductId { get; set; }

    [Required]
    public string MeteredComponentHandle { get; set; } = string.Empty;

    public int MeteredComponentId { get; set; }

    /// <summary>Test hosts set this via PostConfigure so no test makes a real outbound call to Maxio at startup (quality-gate.md J5).</summary>
    public bool SkipStartupValidation { get; set; }

    public string ResolveBaseUrl() => Environment.Equals("EU", System.StringComparison.OrdinalIgnoreCase)
        ? $"https://{Subdomain}.ebilling.maxio.com"
        : $"https://{Subdomain}.chargify.com";
}
