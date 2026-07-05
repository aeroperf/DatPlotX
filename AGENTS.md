# AGENTS.md

Instructions for AI coding agents (Claude Code, Cursor, Codex, Aider, Continue, etc.) working in this repository.

## Project

**DatPlotX** — cross-platform scientific data visualization tool for flight test and time-series data. Three plot rendering modes per project (locked at creation): **Stacked Panes** (synchronized stripchart, ScottPlot), **Compact Plot Surface** (single area with banded Y axes, OxyPlot — FDA / FDM style), and **Grouped Parameter Plot** (one line per input combination, ScottPlot — parametric / lookup-table data).

Licensed MIT. No CLA — contributions are licensed under the same MIT terms (inbound = outbound). See [`CONTRIBUTING.md`](CONTRIBUTING.md).

- Stack: .NET 10, Avalonia 11.x, ScottPlot 5.x, OxyPlot 2.x, CommunityToolkit.Mvvm
- Platforms: Windows, macOS, Linux
- Current version: see `Directory.Build.props`

## Repository Layout

| Path | Purpose | Status |
|------|---------|--------|
| `DatPlotX/` | Avalonia application | **Active development** |
| `DatPlotX.Tests/` | xUnit tests for DatPlotX | Active |
| `DatPlotX.Design/` | Design-time / preview assets | Active |
| `DatPlotX.Website/` | Marketing/docs site (Astro) | Active |
| `Docs/` | User-facing docs, plans, security baseline | Active |
| `scripts/` | Build/release helpers | Active |
| `Images/` | Screenshots, icons | Static |
| `DatPlot.Modern/`, `DatPlot.Modern.Tests/` | Legacy WPF app + its tests | **Frozen — do not modify** |

> **Focus on `DatPlotX/` only.** `DatPlot.Modern/` is the superseded WPF predecessor, kept for reference. It often contains parallel files (e.g. `Services/Parsers/XPlaneDataParser.cs`) — do **not** edit, port fixes to, or build against it unless the user explicitly asks. All new work targets the Avalonia `DatPlotX` app.

## Detailed Agent Instructions

The authoritative deep-dive instructions for the active codebase live in:

- [`DatPlotX/CLAUDE.md`](DatPlotX/CLAUDE.md) — architecture, data flow, MVVM patterns, DataGrid quirks, conventions

Read that file before making changes inside `DatPlotX/`.

## Build, Run, Test

```bash
# Build active app
dotnet build DatPlotX/DatPlotX.csproj

# Run
dotnet run --project DatPlotX

# Test
dotnet test DatPlotX.Tests/DatPlotX.Tests.csproj

# Release build (per-RID self-contained)
dotnet publish DatPlotX/DatPlotX.csproj -c Release -r osx-arm64 --self-contained true
```

Packaging scripts: [`DatPlotX/build-macos-app.sh`](DatPlotX/build-macos-app.sh), [`DatPlotX/build-win-x64.ps1`](DatPlotX/build-win-x64.ps1).

## Conventions (project-wide)

- Strict MVVM — code-behind only for view wiring, no business logic
- Avalonia compiled bindings (`x:DataType`) preferred over reflection bindings
- Async/await for all I/O — no `.Result`, no `.Wait()`, no `async void` outside event handlers
- Nullable reference types **enabled** repo-wide via [`Directory.Build.props`](Directory.Build.props)
- .NET analyzers enabled at `latest-recommended`; warnings surfaced but not errors (see props comment)
- Follow [`.editorconfig`](.editorconfig) for formatting
- Culture-aware parsing: pass explicit `IFormatProvider` (use `options.Culture`), never rely on current culture
- Security: all file paths through `FilePathValidator`, all column names through `InputValidator`

## What NOT to Do

- Do **not** add Windows-only dependencies — DatPlotX must build and run on Windows, macOS, and Linux
- Do **not** introduce `BinaryFormatter` or other unsafe serializers
- Do **not** bypass `FilePathValidator` / `InputValidator` for "convenience"
- Do **not** auto-plot on data import — user controls curve selection (see CLAUDE.md "Add Curves Dialog")
- Do **not** bind `ItemsSource` directly on the source-data DataGrid — use the `RebuildDataGridColumns` code-behind path (see CLAUDE.md "DataGrid")
- Do **not** assume a top-level `MainWindowViewModel.PlotModel` exists — it was removed; use `Panes[i].PlotModel` (Stacked), `CompactPlot.PlotModel` (Compact), or `GroupedPlot` (Grouped)
- Do **not** assume Panes mode in new code. Anything touching plot rendering must respect `IsPanesMode` / `IsCompactMode` / `IsGroupedMode`. Plot mode is locked per project (see DatPlotX/CLAUDE.md "Compact Plot Surface" and "Grouped Parameter Plot")
- Do **not** call `CompactPlotViewModel.Rebuild()` from outside the VM — every mutator already calls it
- Do **not** raise `IAnalysisService.ResultsChanged` (or the curve-source `CurvesChanged` / `VisibleRangeChanged`) off the UI thread — `AnalysisService` snapshots live curve data on the calling thread before its background compute, and the panel mutates `ObservableCollection`s in the handler. Route pan/zoom notifications through `MainWindowViewModel.NotifyAnalysisVisibleRangeChanged` (debounced); see [`Docs/Curve-Analysis-Phase2-Plus-Plan.md`](Docs/Curve-Analysis-Phase2-Plus-Plan.md)

## Versioning & Release

- Version centralized in [`Directory.Build.props`](Directory.Build.props) (`<Version>`)
- Bump version + update [`CHANGELOG.md`](CHANGELOG.md) + update `Docs/` What's New for every release
- Run release pipeline via the `/release` skill (Claude Code) — see [`scripts/`](scripts/)

## Key Reference Documents

| Document | Use when |
|----------|----------|
| [`DatPlotX/CLAUDE.md`](DatPlotX/CLAUDE.md) | Working inside `DatPlotX/` — architecture, MVVM patterns, Compact Plot Surface, DataGrid quirks, conventions |
| [`Docs/Curve-Analysis-Phase2-Plus-Plan.md`](Docs/Curve-Analysis-Phase2-Plus-Plan.md) | Extending the Curve Analysis & Statistics subsystem (shipped Phase 1 in 0.13.0) — engine layout, how Stacked is wired, Phase 2A Compact/Grouped wiring steps, the curve-source base refactor, and the metric-contract tech debt |
| [`Docs/security-baseline.md`](Docs/security-baseline.md) | Security posture and controls in force (path validation, input sanitization, safe serialization, resource limits, local-only observability) |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Contribution workflow and coding conventions |
| [`SECURITY.md`](SECURITY.md) | How to report a vulnerability (private, never a public issue) |
| [`CHANGELOG.md`](CHANGELOG.md) | Release history |

## Data & Test Fixtures

- CSV format: comment lines start with `#`, first non-comment row = headers
- Unit tests generate CSV content inline (see `WriteTempCsv()` in `DatPlotX.Tests/Services/Parsers/CsvDataParserTests.cs`) — no external fixture files
- Configurable limits: 1 GB max file, 10M rows, 5000 columns (`ApplicationSettings`)

## Project File Format

`.DPX` files = GZip-compressed JSON. Contains pane layouts, curves, event lines, axis ranges, annotations. JSON-only serialization (never BinaryFormatter).
