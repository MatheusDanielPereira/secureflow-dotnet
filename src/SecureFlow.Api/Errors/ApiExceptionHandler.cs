using System.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using SecureFlow.Application.Common;
using SecureFlow.Domain.Common;

namespace SecureFlow.Api.Errors;

public sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail, code) = MapException(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception);
        }
        else
        {
            LogRejectedRequest(logger, status, code);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing the request.")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Request rejected with {StatusCode} and error code {ErrorCode}.")]
    private static partial void LogRejectedRequest(
        ILogger logger,
        int statusCode,
        string errorCode);

    private static (int Status, string Title, string Detail, string Code) MapException(
        Exception exception)
    {
        return exception switch
        {
            EntityNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                notFound.Message,
                "resource.not_found"),
            ConcurrencyConflictException conflict => (
                StatusCodes.Status412PreconditionFailed,
                "Concurrency conflict",
                conflict.Message,
                "concurrency.version_mismatch"),
            DomainException domain when domain.Code.StartsWith("validation.", StringComparison.Ordinal) => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                domain.Message,
                domain.Code),
            DomainException domain when domain.Code.StartsWith("authorization.", StringComparison.Ordinal) => (
                StatusCodes.Status403Forbidden,
                "Operation forbidden",
                domain.Message,
                domain.Code),
            DomainException domain => (
                StatusCodes.Status409Conflict,
                "Workflow conflict",
                domain.Message,
                domain.Code),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An unexpected error occurred.",
                "server.unexpected_error")
        };
    }
}
