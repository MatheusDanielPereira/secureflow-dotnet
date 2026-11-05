using System.Collections.Concurrent;

using SecureFlow.Application.Abstractions;
using SecureFlow.Domain.Auditing;

namespace SecureFlow.Infrastructure.Auditing;

public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> ListAsync(
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entries = _entries
            .Reverse()
            .Take(Math.Clamp(take, 1, 500))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AuditEntry>>(entries);
    }
}
