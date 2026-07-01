using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Raised when a lifecycle/plan-change action is requested from a state that does not permit it.</summary>
public class InvalidSubscriptionStateException : Exception
{
    public InvalidSubscriptionStateException(string message) : base(message)
    {
    }
}
