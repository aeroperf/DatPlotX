# Security Baseline — DatPlotX

This document describes the security posture and the controls currently in force in
**DatPlotX**, the cross-platform Avalonia/.NET 10 application in this repository.

> **History:** An earlier security analysis was written for the legacy WPF
> application (DatPlot.Modern), which is not part of this open-source repository.
> This baseline supersedes it and applies to DatPlotX only.

## Threat model

DatPlotX is a **local desktop application**. It has no server component, opens no
listening sockets, and makes no outbound network calls during normal operation.
The primary trust boundary is **untrusted input files** opened by the user:

- Data files — CSV / TSV.
- Project files — `.DPX` (GZip-compressed JSON).

A secondary concern is **local data hygiene** — ensuring logs and crash dumps never
leak sensitive content off the machine.

## Controls in force

### 1. Path-traversal protection (CWE-22)

All file paths pass through [`Helpers/FilePathValidator`](../DatPlotX/Helpers/FilePathValidator.cs)
before use:

- `ValidateAndNormalizePath` normalizes via `Path.GetFullPath` and **rejects `..`
  path segments** and `~` home-directory expansion, throwing `SecurityException`.
- `ValidatePathForSave` / `ValidatePathForLoad` wrap the save/load entry points.

### 2. Input validation & sanitization

[`Helpers/InputValidator`](../DatPlotX/Helpers/InputValidator.cs) validates and
sanitizes user-/file-supplied strings before they reach the data model or UI:

- `ValidateFileName`, `ValidateColumnName`, `ValidateLabel` enforce length caps and
  reject control/injection characters.
- `SanitizeColumnName` + `MakeUniqueColumnNames` produce safe, de-duplicated column
  identifiers from arbitrary CSV headers.
- `ValidatePositiveInteger` / `ValidateDouble` bound numeric inputs.

### 3. Safe deserialization

- Project files are **JSON only**, compressed with GZip. `System.Text.Json` is used
  for (de)serialization.
- **`BinaryFormatter` and other unsafe serializers are not used anywhere** in the
  codebase, and introducing them is explicitly disallowed by the contribution rules.
- The on-disk format carries a `SchemaVersion` field; the loader normalizes legacy
  files and is the single place future migrations are added.

### 4. Resource-exhaustion limits

Configurable hard caps in [`Models/ApplicationSettings`](../DatPlotX/Models/ApplicationSettings.cs)
guard against malicious or accidental oversized inputs:

| Limit | Default |
|-------|---------|
| Max file size | 1 GB |
| Max row count | 10,000,000 |
| Max column count | 5,000 |
| Grouped-plot distinct values | 5,000 |
| Grouped-plot line cap | 48 |

### 5. Local-only observability — no exfiltration

DatPlotX's logging and crash reporting are **strictly local and never uploaded**
(see [privacy.md](privacy.md)):

- **Logging** (`Services/Logging/FileLoggerProvider`) writes rolling daily files to a
  per-OS app data directory (50 MB cap, 7-day retention). It logs **events and errors
  only — never row data, column names, or file contents**; paths are reduced to
  basenames.
- **Crash dumps** (`Services/CrashReporter`) are written locally on unhandled
  exceptions. They carry the stack trace, exception type/message, app version, and OS
  only — never imported rows or source-file contents. `Scrub()` reduces absolute paths
  to their basename; it does **not** rewrite the exception *message*, so a parse/format
  exception may still quote a single cell value or column name in its text. That text
  never leaves the machine — the opt-in `CrashReportingEnabled` setting (OFF by default)
  governs **only** the next-launch "we found a crash report" prompt, and **nothing is
  ever transmitted**.

## Build & dependency hygiene

- Nullable reference types enabled repo-wide; .NET analyzers at `latest-recommended`.
- CI runs `dotnet build` + `dotnet test` + `dotnet format --verify-no-changes` on
  Windows, macOS, and Linux.
- All bundled dependencies are permissively licensed.

## Reporting a vulnerability

See [SECURITY.md](../SECURITY.md) — report privately to **dev@aeroperf.com**, never
via a public issue.
