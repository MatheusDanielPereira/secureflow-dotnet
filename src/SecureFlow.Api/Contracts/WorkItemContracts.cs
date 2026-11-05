namespace SecureFlow.Api.Contracts;

public sealed record CreateWorkItemRequest(string? Title, string? Description);

public sealed record RejectWorkItemRequest(string? Reason);
