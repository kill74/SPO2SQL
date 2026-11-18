# Implementation Status

## Current state (v2.0)

The .NET 8 rewrite is complete and the legacy .NET Framework snapshot in
`SPOtoSQL-Snapshots/` is kept only as a historical reference.

| Area | Status |
| --- | --- |
| Host, dependency injection, options validation | Done |
| Structured logging bridged from the Core library | Done |
| SharePoint paging and cancellation | Done |
| Transaction scoped to the write phase | Done |
| UTC watermark for incremental syncs | Done |
| Unified `SyncSettings` passed to the sync layer | Done |
| App-only authentication with certificate | Done |
| Real SharePoint/SQL startup health checks | Done |
| `--setup-secrets` in the real console host | Done |
| End-to-end async (CSOM, ADO.NET, retries, DQ) | Done |
| Nullable reference types enforced without blanket suppressions | Done |
| CI on Linux and Windows with format and NuGet audit | Done |

## Validation

```bash
dotnet restore SPO2SQL.sln
dotnet build SPO2SQL.sln --no-restore -warnaserror
dotnet test SPO2SQL.sln --no-build --no-restore
dotnet format SPO2SQL.sln --verify-no-changes --no-restore
```

The build is warning-free; the unit test suite covers CAML generation, retry
behaviour (sync and async), configuration binding/validation, XML list mappings,
logging routing, and exit-code propagation.

## Remaining work

- Integration tests against a real SharePoint tenant and SQL Server.
- Observability export (OpenTelemetry) behind `Application:EnableMetrics`.
- Replace the in-house `RetryPolicy` with a Polly resilience pipeline.
