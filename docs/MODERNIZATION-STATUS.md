# Modernization Status

**Last updated:** 2026-11-18

## Summary

The modernization plan from the original migration documents has been completed.
The previous phase percentages in this document were snapshots of an older state
and no longer describe the codebase.

| Original phase | Outcome |
| --- | --- |
| Foundation & infrastructure | Completed: .NET 8 host, DI, options, multi-source configuration |
| Core services modernization | Completed: async SharePoint/SQL/DQ pipeline, cancellation, paging |
| Modern C# features | Completed: records, pattern matching, file-scoped namespaces, nullable enabled |
| Architecture | Completed: `SyncSettings`, credential chain, health checks, retry policy |
| Security | Completed: app-only certificate authentication, encrypted connection enforcement |
| Testing | Unit suite in place; live integration tests remain environment-specific |
| Documentation | README, configuration guide and security guide updated |

## Architecture today

```
src/SPO2SQL.Console   Host, options, validation, health checks, exit code, CLI setup
src/SPO2SQL.Core      SharePoint access, SQL transfer, credentials, XML list mappings
tests/                xUnit unit tests for both projects
```

The host maps validated options into `SyncSettings` once and passes it through the
sync pipeline. The legacy XML file is used only for list/column mappings; secrets
come from the credential provider chain (environment, DPAPI on Windows, or the
values supplied through configuration/user secrets).

## Known limitations

- Username/password authentication is retained as a fallback and will not work in
  tenants that require interactive MFA. Use app-only authentication instead.
- Metrics are logged per list; there is no metrics exporter yet.
- `dotnet format` expects LF line endings, enforced for C# files in `.gitattributes`.
