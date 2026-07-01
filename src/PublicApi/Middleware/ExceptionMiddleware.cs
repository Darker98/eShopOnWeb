using System;
using System.Net;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.PublicApi.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex);        
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // Every branch below carries a message that was already curated to be safe for an API
        // response at the point the exception was constructed (see each type's constructor, and
        // MaxioBillingClient's message curation for BillingProviderException) - unlike the generic
        // catch-all, which is a pre-existing gap this middleware does not otherwise touch (quality-gate.md H2).
        if (TryResolveSubscriptionStatusCode(exception, out var statusCode))
        {
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = exception.Message
            }.ToString());
        }
        else if (exception is DuplicateException duplicationException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = duplicationException.Message
            }.ToString());
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = exception.Message
            }.ToString());
        }
    }

    private static bool TryResolveSubscriptionStatusCode(Exception exception, out int statusCode)
    {
        statusCode = exception switch
        {
            SubscriptionNotFoundException => (int)HttpStatusCode.NotFound,
            StalePlanChangePreviewException => (int)HttpStatusCode.Conflict,
            InvalidSubscriptionStateException => (int)HttpStatusCode.UnprocessableEntity,
            // A misconfigured billing-provider component is an operator/config problem, not the caller's fault.
            MeteredComponentMisconfiguredException => (int)HttpStatusCode.InternalServerError,
            BillingProviderException { HttpStatusCode: >= 400 and < 500 } => (int)HttpStatusCode.UnprocessableEntity,
            BillingProviderException => (int)HttpStatusCode.BadGateway,
            _ => 0
        };

        return statusCode != 0;
    }
}
