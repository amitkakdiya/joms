using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Joms.Middleware;

internal sealed class OrderWorkflowExceptionHandler(
    ILogger<OrderWorkflowExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not SqlException { Number: >= 50001 and <= 50023 } sqlException)
            return false;

        var statusCode = sqlException.Number is 50011 or 50012 or 50014
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status409Conflict;
        var title = statusCode == StatusCodes.Status400BadRequest ? "Invalid request" : "Order workflow conflict";
        logger.LogWarning(exception, "Order workflow request failed with database error {ErrorNumber}.", sqlException.Number);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status400BadRequest
                ? "The request contains invalid workflow data."
                : "The order status or assignment does not allow this action.",
            Instance = httpContext.Request.Path
        }, cancellationToken);
        return true;
    }
}
