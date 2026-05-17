## 2024-05-24 - SQL Injection in DDL Fix
**Vulnerability:** A SQL injection vulnerability existed in `SQLInteraction.cs` where user-provided datatype configurations (`mapping.DataType`) from an XML file were directly concatenated into `CREATE TABLE` and `ALTER TABLE` commands.
**Learning:** DDL operations cannot be parameterized, so data inputs need to be sanitized manually.
**Prevention:** Always sanitize dynamic inputs (e.g. replacing `;`, `'`, and `--`) or use a strict allowlist of permitted SQL server datatypes when constructing DDL queries.
