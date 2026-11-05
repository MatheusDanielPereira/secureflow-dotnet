# Architecture and security notes

## Design goals

SecureFlow favors explicit business rules and small, replaceable adapters. The domain has no dependency on ASP.NET Core, authentication libraries, persistence providers, or configuration frameworks.

## Trust boundaries

1. The API treats every request, header, token, and JSON body as untrusted input.
2. The configured OIDC provider establishes the caller identity. Authorization policies then evaluate explicit scopes.
3. The application layer receives a stable subject identifier rather than the complete token.
4. The domain enforces resource-level invariants such as ownership and separation of duties.
5. Infrastructure adapters are responsible for atomic version checks and append-only audit storage.

Authentication and authorization are deliberately separate. A valid token without the required scope receives `403 Forbidden`; a request without an authenticated identity receives `401 Unauthorized`.

## Concurrency model

Each work item starts at version 1. Every successful transition produces a new immutable record with an incremented version. Clients send the version as a strong ETag in `If-Match`. Both the application service and repository verify it, protecting against stale reads and races between concurrent writers.

## Error model

Expected failures are returned as problem details with a stable machine-readable `code` and a request `traceId`. Unexpected exception messages are logged server-side but are never returned to the caller.

## Audit model

Audit entries capture the work item, event name, actor subject, UTC timestamp, and resulting version. They intentionally avoid request bodies, access tokens, email addresses, or other unnecessary personal data.

The in-memory adapter is suitable only for local evaluation. A production adapter should use append-only durable storage, restricted write permissions, encryption, retention policies, centralized monitoring, and an independently protected time source.

## Security decisions

- No embedded authentication shortcut or sample signing key
- No committed connection strings, credentials, tenant IDs, tokens, or personal data
- HTTPS metadata required for OIDC by default
- OpenAPI published only in Development
- No server technology header
- 32 KiB maximum request body
- Rate limiting by subject or remote address
- One-minute maximum token clock skew
- Security headers appropriate for a JSON API
- Constant generic response for unexpected server errors

## Production hardening checklist

- Configure a dedicated OIDC tenant, audience, and least-privilege application roles
- Store configuration in a managed secret service
- Configure trusted reverse proxies and forwarded headers explicitly
- Replace in-memory adapters with durable transactional storage
- Export structured logs, traces, and metrics to a protected observability platform
- Add centralized audit retention and anomaly alerts
- Enforce dependency, container, and source scanning in the delivery pipeline
- Use workload identity and short-lived credentials
- Run load, resilience, and penetration tests before release
