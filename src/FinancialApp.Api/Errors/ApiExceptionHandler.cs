using Microsoft.AspNetCore.Diagnostics;

namespace FinancialApp.Api.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        this.logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            "Unhandled API request failure of type {ExceptionType}. Trace ID: {TraceId}",
            exception.GetType().Name,
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                error = "The request could not be completed.",
                traceId = httpContext.TraceIdentifier,
            },
            cancellationToken);

        return true;
    }
}
