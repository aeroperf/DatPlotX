# DatPlotX Roadmap

This is a living document of features that are **designed but not yet shipped**. It exists so the
direction is visible and so early feedback can shape priorities. Nothing here is a commitment or a
dated promise — order and scope will shift based on what users actually need.

**Want something on this list sooner, or something not here at all?** Open a
[Discussion](https://github.com/aeroperf/Datplot/discussions) or an issue. Real use cases move items
up.

DatPlotX has three plot rendering modes, locked per project at creation:

- **Stacked Panes** — synchronized stripchart (ScottPlot)
- **Compact Plot Surface** — single area with banded Y axes (OxyPlot), FDA / FDM style
- **Grouped Parameter Plot** — one line per input combination (ScottPlot), parametric / lookup-table data

Stacked and Compact are time-series focused; Grouped serves parametric / lookup-table data. They
deliberately differ — some features (event lines, for instance) only make sense for time-series — so
the roadmap is about deepening each mode where it matters, not forcing identical feature sets across
all three.

---

## Later

- **Log-scale axes** — per-axis independent X/Y log toggle. Use cases: drag coefficient vs Reynolds
  number, pressure/density altitude, frequency-domain plots. Non-positive values handled gracefully.
- **Copy to clipboard** — copy the current plot as PNG or SVG; optionally copy the visible data range
  as CSV.
- **PDF image export** — likely lands alongside a print path, since the two share most of the
  rendering work.
- **Curve reordering UI** in Compact and Grouped (Stacked already supports drag-reorder).
- **Compact mode polish** — legend toggle and a per-curve right-click menu to match Stacked.
- **Grid-line styling UI** for Grouped.

---

## Auto-update

Currently DatPlotX is distributed as self-contained per-platform downloads with no built-in update
mechanism. Planned:

- **In-app update notification** when a newer version is available, with one-click download + apply
  on explicit user consent.
- **Opt-in / opt-out** toggle in Settings; off until the user accepts.
- **Cross-platform** — Windows and macOS first-class (in-place `.app` replacement), Linux best-effort
  (AppImage).
- **Signed update payloads** — cryptographic signature verification, not just a hash.
- Works without an app store and behind HTTPS-only corporate proxies.

Likely built on [Velopack](https://github.com/velopack/velopack) (MIT, .NET-native) with GitHub
Releases as the artifact host. **Non-goals for a first version:** delta patching, silent background
updates, and staged/canary rollout channels — a single stable channel to start, with room to add a
beta channel later.

Making auto-update pleasant depends on **code signing / notarization** (Windows Authenticode, macOS
Developer ID + notarization) so updates don't trip SmartScreen / Gatekeeper. That work is tracked
alongside this.

---

## Other candidates (unscheduled)

These come up but aren't designed yet — feedback welcome on whether they matter to you:

- **Print** support (shares rendering work with PDF export)
- **Drag-and-drop** file import
- **Undo / redo**
- High-DPI rendering sweep

---

*Have a use case that isn't served? That's exactly the feedback that shapes this list — open a
[Discussion](https://github.com/aeroperf/Datplot/discussions).*
