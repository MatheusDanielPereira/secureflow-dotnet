using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using SecureFlow.Api.Contracts;
using SecureFlow.Api.Tests.Infrastructure;
using SecureFlow.Application.WorkItems;
using SecureFlow.Domain.Auditing;
using SecureFlow.Domain.WorkItems;

namespace SecureFlow.Api.Tests.Endpoints;

public sealed class SecureFlowApiTests(SecureFlowApiFactory factory)
    : IClassFixture<SecureFlowApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Health_IsAnonymousAndIncludesDefensiveHeaders()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task CorrelationId_AcceptsSafeCallerValue()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add("X-Correlation-ID", "candidate-123");

        using var response = await client.SendAsync(request);

        Assert.Equal("candidate-123", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task WorkItems_RejectAnonymousRequests()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/work-items?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsResourceLocationAndEntityTag()
    {
        using var client = CreateAuthenticatedClient("owner-create", "workflow.write");

        using var response = await client.PostAsJsonAsync(
            "/api/work-items/",
            new CreateWorkItemRequest("Security review", "Review the quarterly controls."));
        var workItem = await response.Content.ReadFromJsonAsync<WorkItemResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(workItem);
        Assert.Equal(WorkItemStatus.Draft, workItem.Status);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        Assert.Equal($"/api/work-items/{workItem.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Submit_RequiresIfMatchHeader()
    {
        using var client = CreateAuthenticatedClient("owner-precondition", "workflow.write");
        var workItem = await CreateWorkItemAsync(client, "Precondition check");

        using var response = await client.PostAsync(
            $"/api/work-items/{workItem.Id}/submit",
            null);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Fact]
    public async Task Approval_EnforcesSeparationOfDuties()
    {
        using var client = CreateAuthenticatedClient(
            "owner-separation",
            "workflow.write workflow.approve");
        var workItem = await CreateWorkItemAsync(client, "Separation of duties");
        using var submitResponse = await SendVersionedAsync(
            client,
            $"/api/work-items/{workItem.Id}/submit",
            workItem.Version);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<WorkItemResult>(JsonOptions);

        using var approvalResponse = await SendVersionedAsync(
            client,
            $"/api/work-items/{workItem.Id}/approve",
            submitted!.Version);

        Assert.Equal(HttpStatusCode.Forbidden, approvalResponse.StatusCode);
    }

    [Fact]
    public async Task Workflow_HappyPathCreatesAuditTrail()
    {
        using var ownerClient = CreateAuthenticatedClient("owner-happy", "workflow.write");
        var workItem = await CreateWorkItemAsync(ownerClient, "Quarterly access review");
        using var submitResponse = await SendVersionedAsync(
            ownerClient,
            $"/api/work-items/{workItem.Id}/submit",
            workItem.Version);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<WorkItemResult>(JsonOptions);

        using var approverClient = CreateAuthenticatedClient(
            "approver-happy",
            "workflow.approve workflow.audit");
        using var approveResponse = await SendVersionedAsync(
            approverClient,
            $"/api/work-items/{workItem.Id}/approve",
            submitted!.Version);
        var approved = await approveResponse.Content.ReadFromJsonAsync<WorkItemResult>(JsonOptions);
        using var auditResponse = await approverClient.GetAsync("/api/audit-events?take=20");
        var auditEntries = await auditResponse.Content.ReadFromJsonAsync<AuditEntry[]>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(WorkItemStatus.Approved, approved!.Status);
        Assert.Equal("approver-happy", approved.DecidedBy);
        Assert.Contains(
            auditEntries!,
            entry => entry.WorkItemId == workItem.Id && entry.EventName == "work-item.approved");
    }

    [Fact]
    public async Task Update_WithStaleVersionReturnsPreconditionFailed()
    {
        using var ownerClient = CreateAuthenticatedClient("owner-stale", "workflow.write");
        var workItem = await CreateWorkItemAsync(ownerClient, "Concurrent update");
        using var submitResponse = await SendVersionedAsync(
            ownerClient,
            $"/api/work-items/{workItem.Id}/submit",
            workItem.Version);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        using var approverClient = CreateAuthenticatedClient("approver-stale", "workflow.approve");
        using var response = await SendVersionedAsync(
            approverClient,
            $"/api/work-items/{workItem.Id}/approve",
            workItem.Version);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutWriteScopeIsForbidden()
    {
        using var client = CreateAuthenticatedClient("reader-only", "workflow.read");

        using var response = await client.PostAsJsonAsync(
            "/api/work-items/",
            new CreateWorkItemRequest("Forbidden create", "Missing write scope."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string userId, string scopes)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, scopes);
        return client;
    }

    private static async Task<WorkItemResult> CreateWorkItemAsync(
        HttpClient client,
        string title)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/work-items/",
            new CreateWorkItemRequest(title, "Clean-room demonstration request."));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkItemResult>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> SendVersionedAsync(
        HttpClient client,
        string requestUri,
        long version)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return client.SendAsync(request);
    }
}
