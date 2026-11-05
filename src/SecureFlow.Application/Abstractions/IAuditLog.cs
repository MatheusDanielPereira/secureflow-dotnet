using SecureFlow.Domain.Auditing;

namespace SecureFlow.Application.Abstractions;

public interface IAuditLog
{
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntry>> ListAsync(int take, CancellationToken cancellationToken);
}
