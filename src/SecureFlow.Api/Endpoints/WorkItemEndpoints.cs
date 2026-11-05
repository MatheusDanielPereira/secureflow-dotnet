using System.Globalization;
using System.Security.Claims;

using SecureFlow.Api.Contracts;
using SecureFlow.Api.Security;
using SecureFlow.Application.WorkItems;
using SecureFlow.Domain.WorkItems;

namespace SecureFlow.Api.Endpoints;

public static class WorkItemEndpoints
{
    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/work-items")
            .WithTags("Work items")
            .RequireRateLimiting("api");

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("CreateWorkItem")
            .Produces<WorkItemResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListAsync)
            .RequireAuthorization(AuthorizationPolicies.Read)
            .WithName("ListWorkItems")
            .Produces<PagedResult<WorkItemResult>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(AuthorizationPolicies.Read)
            .WithName("GetWorkItem")
            .Produces<WorkItemResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/submit", SubmitAsync)
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("SubmitWorkItem")
            .Produces<WorkItemResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        group.MapPost("/{id:guid}/approve", ApproveAsync)
            .RequireAuthorization(AuthorizationPolicies.Approve)
            .WithName("ApproveWorkItem")
            .Produces<WorkItemResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        group.MapPost("/{id:guid}/reject", RejectAsync)
            .RequireAuthorization(AuthorizationPolicies.Approve)
            .WithName("RejectWorkItem")
            .Produces<WorkItemResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateWorkItemRequest request,
        ClaimsPrincipal user,
        WorkflowService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            new CreateWorkItemCommand(request.Title, request.Description, GetActor(user)),
            cancellationToken);

        SetEntityTag(httpContext.Response, result.Version);
        return Results.Created($"/api/work-items/{result.Id}", result);
    }

    private static Task<PagedResult<WorkItemResult>> ListAsync(
        WorkItemStatus? status,
        int? page,
        int? pageSize,
        WorkflowService service,
        CancellationToken cancellationToken)
    {
        return service.ListAsync(status, page ?? 1, pageSize ?? 20, cancellationToken);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        WorkflowService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        SetEntityTag(httpContext.Response, result.Version);
        return Results.Ok(result);
    }

    private static async Task<IResult> SubmitAsync(
        Guid id,
        ClaimsPrincipal user,
        WorkflowService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetExpectedVersion(httpContext.Request, out var expectedVersion))
        {
            return PreconditionRequired(httpContext);
        }

        var result = await service.SubmitAsync(
            id,
            expectedVersion,
            GetActor(user),
            cancellationToken);

        SetEntityTag(httpContext.Response, result.Version);
        return Results.Ok(result);
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        ClaimsPrincipal user,
        WorkflowService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetExpectedVersion(httpContext.Request, out var expectedVersion))
        {
            return PreconditionRequired(httpContext);
        }

        var result = await service.ApproveAsync(
            id,
            expectedVersion,
            GetActor(user),
            cancellationToken);

        SetEntityTag(httpContext.Response, result.Version);
        return Results.Ok(result);
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        RejectWorkItemRequest request,
        ClaimsPrincipal user,
        WorkflowService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetExpectedVersion(httpContext.Request, out var expectedVersion))
        {
            return PreconditionRequired(httpContext);
        }

        var result = await service.RejectAsync(
            id,
            expectedVersion,
            new RejectWorkItemCommand(request.Reason, GetActor(user)),
            cancellationToken);

        SetEntityTag(httpContext.Response, result.Version);
        return Results.Ok(result);
    }

    private static string GetActor(ClaimsPrincipal user)
    {
        return user.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("The authenticated principal has no subject claim.");
    }

    private static bool TryGetExpectedVersion(HttpRequest request, out long expectedVersion)
    {
        var rawValue = request.Headers["If-Match"].ToString().Trim();

        if (rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"')
        {
            rawValue = rawValue[1..^1];
        }

        return long.TryParse(
            rawValue,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out expectedVersion)
            && expectedVersion > 0;
    }

    private static IResult PreconditionRequired(HttpContext httpContext)
    {
        return Results.Problem(
            statusCode: 428,
            title: "Precondition required",
            detail: "Supply the current entity version in the If-Match header.",
            instance: httpContext.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "concurrency.if_match_required",
                ["traceId"] = httpContext.TraceIdentifier
            });
    }

    private static void SetEntityTag(HttpResponse response, long version)
    {
        response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";
    }
}
