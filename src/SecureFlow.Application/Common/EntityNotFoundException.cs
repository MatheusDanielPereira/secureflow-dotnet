namespace SecureFlow.Application.Common;

public sealed class EntityNotFoundException(string entityName, Guid id)
    : Exception($"{entityName} '{id}' was not found.")
{
    public string EntityName { get; } = entityName;

    public Guid EntityId { get; } = id;
}
