# DatPlotX

Cross-platform scientific data visualization tool for flight test and time-series data. Built with Avalonia (.NET 10), runs on Windows, macOS, and Linux.

## What It Does

DatPlotX renders multi-pane synchronized stripchart plots — stacked time-series panels sharing a common X-axis. Designed for analyzing flight test data with multiple parameters simultaneously.

**Key features:**
- **Three plot modes per project**, picked at project creation (locked for the life of the project):
  - **Stacked Panes** (default, ScottPlot): multi-pane synchronized plots with dual Y-axes (Y1 left, Y2 right), curve analysis & statistics, text and arrow annotations
  - **Compact Plot Surface** (OxyPlot, FDA / FDM style): single plot area with one banded Y axis per curve, alternating left/right — modeled on NTSB-style flight data exhibits. Booleans auto-detect onto a narrower band
  - **Grouped Parameter Plot** (ScottPlot): one line per unique combination of selected input-parameter values, for tabular / array-style data (parametric performance arrays, lookup tables)
- **Curve Analysis & Statistics** (Stacked & Compact): dockable Analysis Results panel with 18 metrics (Max / Min / Mean / Median / Std Dev / Variance / RMS / percentiles / Slope w/ derived rate / Integral / …) computed over named analysis segments — visible window, full data, Shift+drag range, or event-line pair. Inline corner overlay available in Stacked. Segments persist in the project file
- CSV / TSV / X-Plane data import with delimiter / decimal / header-line / unit-line / data-start configuration and a first-100-lines preview
- Large-file support (up to 1 GB, 10M rows, 5000 columns)
- Draggable event lines on both surfaces — real-time intersection callouts in Stacked mode
- Project save/load (`.DPX` format — GZip-compressed JSON)
- Image export to PNG, JPEG, BMP, SVG (both Stacked and Compact modes)
- Intersection export to CSV (Stacked mode)
- Hover tooltips snap to nearest point on both surfaces (toggle with Ctrl+T)
- In-app User Guide (F1) and What's New (Shift+F1)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

Verify install:
```bash
dotnet --version
# should show 10.x.x
```

## Running from Command Line

```bash
# Clone or navigate to the repo
cd /path/to/AeroPerf/Datplot/DatPlotX

# Restore packages (first run only)
dotnet restore

# Run (Debug)
dotnet run

# Run (Release)
dotnet run -c Release
```

## Building

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Publish self-contained executable (e.g. for macOS)
dotnet publish -c Release -r osx-arm64 --self-contained true -o ./publish/osx-arm64

# Publish for Windows x64
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish/win-x64

# Publish for Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64
```

Available runtime identifiers: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`.

## Workflow

1. **New project** — File > New Project (Ctrl+N). Pick a plot surface (Stacked Panes or Compact), then choose a CSV / TSV / X-Plane file. The Import Options dialog confirms delimiter, decimal, and which lines hold headers / units / first data row.
2. **Select X-axis** — pick the time / index column from the X-Axis Parameter dropdown at the top right.
3. **Add curves**:
   - **Stacked**: Curves > Add Curves... (Ctrl+Shift+A) — pick parameter and Y axis (Y1 left / Y2 right), Plot Curve. Or right-click a pane > Add Curves to This Pane.
   - **Compact**: Curves > Add Compact Curves... (Ctrl+Shift+A) — two-stage dialog: pick CSV columns first, then per-curve set axis side / color / line style / marker / boolean / overflow.
4. **(Stacked) Add panes** — right-click a pane > Add Pane Below / Remove This Pane.
5. **Event lines**:
   - **Stacked**: Event Lines > Add Event Line (Ctrl+L), or right-click a pane > Add Event Line Here.
   - **Compact**: right-click the surface > Add Event Line Here.
6. **(Stacked) Annotations** — right-click a pane > Add Text / Arrow Annotation Here.
7. **Export** — File > Export Image (Ctrl+E) for PNG / JPEG / BMP / SVG; File > Export Intersections (Ctrl+Shift+E) for the intersection CSV.
8. **Save** — File > Save Project (Ctrl+S) → `.DPX` file. File > Open Recent re-opens recent projects.

**Pan & zoom (both surfaces):** left-drag pans, Ctrl+left-drag rubber-band zooms, mouse wheel / 2-finger swipe zooms, Ctrl/Cmd+wheel fine zoom, double-click or Home resets, right-click opens the context menu. In Compact mode, hover over an axis to zoom that axis only.

## Keyboard Shortcuts

| Action | Shortcut |
|---|---|
| New Project | Ctrl+N |
| Open Project | Ctrl+O |
| Save Project | Ctrl+S |
| Save Project As | Ctrl+Shift+S |
| Export Image | Ctrl+E |
| Export Intersections | Ctrl+Shift+E |
| Exit | Ctrl+Q |
| Add Curves (Stacked / Compact) | Ctrl+Shift+A |
| Manage Curves / Manage Curve | Ctrl+M |
| Clear All Curves | Ctrl+Shift+Delete |
| Add Event Line (Stacked) | Ctrl+L |
| Clear All Event Lines (Stacked) | Ctrl+Shift+L |
| Analysis Results Panel (Stacked & Compact) | Ctrl+R |
| Inline Metrics Overlay (Stacked) | Ctrl+I |
| Toggle Hover Tooltips | Ctrl+T |
| Format Pane (Compact) | Ctrl+F |
| Settings | Ctrl+, |
| User Guide | F1 |
| What's New | Shift+F1 |
| Close dialog (Cancel) | Esc |

On macOS, Ctrl maps to the Command key. Destructive clears (Clear All Curves, Clear All Event Lines) prompt for confirmation.

## Project File Format

Projects are saved as `.DPX` files: JSON serialized and GZip compressed. Contains all pane layouts, curves, event lines, axis ranges, and annotations.

## Data Format

DatPlotX imports:
- **CSV/TSV** — first row must be column headers; delimiter auto-detected or configured via Import Options
- **X-Plane** — X-Plane flight data recorder format

Configurable limits (in `ApplicationSettings`):
| Setting | Default |
|---------|---------|
| Max file size | 1 GB |
| Max rows | 10,000,000 |
| Max columns | 5,000 |

## Architecture

```
Views (AXAML + code-behind)
    ↓ data binding
ViewModels (CommunityToolkit.Mvvm)
    ↓ interfaces
Services (business logic, I/O, calculations)
    ↓
Models (plain data, ScottPlot.Plot)
```

**Key patterns:** MVVM, Dependency Injection, Strategy (image export), SOLID principles.

**UI framework:** Avalonia 11.x with Fluent theme  
**Charting:** ScottPlot 5.x (Stacked Panes + Grouped Parameter modes), OxyPlot 2.x (Compact Plot Surface mode)  
**CSV parsing:** CsvHelper 33.x

## Solution Structure

```
DatPlotX/
├── App.axaml(.cs)          # App startup, DI registration
├── Program.cs              # Entry point
├── Models/                 # Data structures
├── Services/               # Business logic, file I/O, export
│   ├── Analysis/           # Metric engine, registry, AnalysisService, curve sources
│   ├── Parsers/            # CSV and X-Plane parsers
│   └── Export/             # PNG, JPEG, BMP, SVG strategies
├── ViewModels/             # MVVM presentation logic
│   └── PlotPane/           # Per-pane managers (curves, annotations)
├── Views/                  # AXAML UI and code-behind
│   └── Controls/           # Input, drag, hit-test handlers
├── Helpers/                # Security validators, error handling
└── Converters/             # XAML value converters
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.3.12 | Cross-platform UI framework |
| ScottPlot.Avalonia | 5.1.57 | Charting for Stacked Panes mode |
| OxyPlot.Core | 2.2.0 | Charting core for Compact Plot Surface |
| OxyPlot.Avalonia | 2.1.0-Avalonia11 | Avalonia 11 host for OxyPlot (no stable build yet) |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM source generators |
| CsvHelper | 33.0.1 | CSV/TSV parsing |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | DI container |

## Security

Input validation and file operation security follow OWASP/CWE guidelines:
- Path traversal prevention (CWE-22) via `FilePathValidator`
- Input sanitization / CSV injection prevention (CWE-20) via `InputValidator`
- Sanitized error messages (CWE-209) via `SafeErrorHandler`
- File size limits to prevent resource exhaustion (CWE-400)
- Decompression-bomb guard on `.DPX` project load (CWE-409)
- JSON-only serialization (no `BinaryFormatter`)

## Privacy

DatPlotX makes no network calls — no telemetry, no analytics, no account. Logs and
optional crash dumps are written to a per-OS folder (openable from the Help menu),
record events and stack traces only — never row data, column names, or file
contents — and are never uploaded.

## License

MIT — see [`LICENSE`](../LICENSE). Contributions are inbound = outbound under the
same MIT terms; no CLA. See [`CONTRIBUTING.md`](../CONTRIBUTING.md).
