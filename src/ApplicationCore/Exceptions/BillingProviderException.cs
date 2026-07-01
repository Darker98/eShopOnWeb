using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when the billing provider rejects a request or is unreachable. <see cref="Message"/> is
/// a safe, user-facing summary; <see cref="ProviderMessages"/> carries the provider's own error
/// strings for logging only (see api-integration-quality-gate.md Gate 4 - typed per-operation errors).
/// </summary>
public class BillingProviderException : Exception
{
    public BillingProviderException(string message, IReadOnlyList<string>? providerMessages = null, int? httpStatusCode = null)
        : base(message)
    {
        ProviderMessages = providerMessages ?? Array.Empty<string>();
        HttpStatusCode = httpStatusCode;
    }

    public IReadOnlyList<string> ProviderMessages { get; }
    public int? HttpStatusCode { get; }
}
