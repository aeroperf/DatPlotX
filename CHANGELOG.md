# Changelog

## 1.1.0 — 18 July 2026

Bug-fix and correctness release following the first open-source review pass. No breaking changes.

### Data safety
- Project files are now saved atomically (write to a temporary file, then replace), so a crash or disk-full mid-save can no longer corrupt or truncate an existing `.DPX`.
- Closing the app via the window controls (title-bar close, ⌘Q / Alt+F4) now prompts to save unsaved changes, matching File → Exit. Previously only the menu path prompted, so work could be lost silently.
- A failed save is now reported as a failure instead of "Save cancelled", and the project stays marked unsaved.

### Plot correctness
- Hover tooltips now identify the nearest curve in screen space, fixing the wrong curve (or no tooltip) being shown on panes with both a left (Y1) and right (Y2) axis.
- Box-zoom and drag-pan now move the Y2 axis together with Y1, so curves on the right axis stay in sync.
- One-sided manual axis ranges (only a min, or only a max) are now honored instead of being ignored; inverted ranges (min ≥ max) are rejected with a message.
- Grouped Parameter Plot: "Set Scale to Default" no longer stacks duplicate, undeletable copies of annotations; toggling the legend on before selecting X and Y no longer draws an empty legend box.

### Data import / export
- The Import Options dialog now blocks the invalid combination of a comma column delimiter with a comma decimal separator, which previously produced a garbled import with no error.
- CSV column-type detection now matches the row-fill parser exactly, so values such as accounting-negatives `(1.5)` or currency strings no longer detect as numeric and then silently become empty cells.
- Multi-pane SVG export now produces genuine vector SVG instead of PNG bytes written into a `.svg` file.
- Tab and comprehensive-report exports now format numbers with the invariant culture, matching the CSV exports (no locale-dependent decimal commas).

### Analysis & statistics
- Compact-mode statistics now sort by the X column before computing, fixing wrong Mean / Integral / tolerance-band results when the X column is not ascending.
- All metrics now skip non-finite (±Infinity) samples, not only NaN, matching the tolerance-band evaluator.
- Standard deviation and variance are now labeled "StdDev (pop)" / "Variance (pop)" to make explicit that they are population statistics (divide by N).
- The slope R² tolerance no longer scales with sample count, fixing a false R² = 1.0 on large-offset, low-variance signals.

### Editing & dialogs
- Curve Manager: Cancel now correctly discards edits; changes to colour, width, style, and visibility are applied only on OK (previously they leaked into the live plot and the saved project).
- Settings dialog changes are no longer silently discarded when there is no owner window.
- Arrow annotations keep their arrowhead length when the size field is not edited.
- Segments can no longer be renamed to a blank name.

### macOS
- Keyboard shortcuts now use ⌘ (Command) instead of Ctrl, and the menus and welcome screen display the ⌘ symbol.

### Other
- Caught errors are now written to the rolling log file (Help → Open Log Folder), not just the debugger, so they survive in Release builds.
- Recent-file entries are path-normalized so the same file opened via different paths no longer appears twice.
- The "Remove This Pane" confirmation now shows the correct 1-based pane number.
- Compact-mode fixes: event-line callouts track the line across data gaps; analysis markers no longer draw on the wrong band's axis; marker/curve colour matching compares colour values rather than hex text.

## 1.0.0 — 6 July 2026

Initial public release of DatPlotX.
