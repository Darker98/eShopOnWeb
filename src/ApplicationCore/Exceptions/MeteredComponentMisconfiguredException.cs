using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when the configured usage component handle does not resolve to a metered-kind component
/// on the product family. Recording usage must be refused before any provider call (plan.md UC2).
/// </summary>
public class MeteredComponentMisconfiguredException : Exception
{
    public MeteredComponentMisconfiguredException(string componentHandle, string actualKind)
        : base($"Component '{componentHandle}' is not a metered component (kind: {actualKind}). Usage cannot be recorded against it.")
    {
    }
}
