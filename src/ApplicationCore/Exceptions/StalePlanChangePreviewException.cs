using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when the proration re-computed immediately before commit no longer matches the amount the
/// customer was previously shown (e.g. the billing-period boundary was crossed in between). See plan.md AC-07b.
/// </summary>
public class StalePlanChangePreviewException : Exception
{
    public StalePlanChangePreviewException()
        : base("The previewed proration is no longer current. Request a new preview before confirming this plan change.")
    {
    }
}
