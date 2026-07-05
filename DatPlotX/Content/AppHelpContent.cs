namespace DatPlotX.Content;

public static class AppHelpContent
{
    public static List<HelpSection> GetSections() =>
    [
        GettingStarted(),
        StackedPanesSurface(),
        CompactPlotSurface(),
        GroupedParameterPlotSurface(),
        FileMenu(),
        CurvesMenu(),
        EventLinesMenu(),
        ToolsMenu(),
        HelpMenu(),
        SourceDataPanel(),
        XAxisParameter(),
        AnnotationsAndCallouts(),
        AnalysisAndStatistics(),
        ImportOptions(),
        KeyboardShortcuts(),
    ];

    private static HelpSection GettingStarted() => new("Getting Started",
    [
        new ParagraphBlock(
            "DatPlotX is a cross-platform desktop application for plotting CSV, TSV, and X-Plane " +
            "data files as 2D curves. Each project uses one of three plot surfaces — Stacked Panes " +
            "(synchronised stripchart), Compact Plot Surface (FDA / FDM-style banded plot), or " +
            "Grouped Parameter Plot (one line per combination of input values, for tabular data). " +
            "The plot surface is chosen at project creation and locked for the life of the project."),
        new SubHeadingBlock("Welcome View"),
        new ParagraphBlock(
            "When DatPlotX launches with no project, the welcome view shows two large entry points: " +
            "New Project (Ctrl+N) and Open Project (Ctrl+O). The plot area and source-data panel " +
            "only appear once a project is active."),
        new SubHeadingBlock("Typical Workflow"),
        new BulletListBlock([
            "File > New Project (Ctrl+N) — pick a plot surface (Stacked Panes, Compact, or Grouped Parameter Plot), then choose a CSV, TSV, or X-Plane data file. The Import Options dialog lets you confirm delimiter, decimal, and which lines hold the headers / units / first data row before the file is parsed.",
            "Stacked / Compact: use the X-Axis Parameter selector at the top right to pick the X column. Grouped: pick the X / Y columns and grouping inputs in the sidebar (or the Configure Inputs wizard, which opens automatically on first import).",
            "Stacked / Compact: Curves > Add Curves... / Add Compact Curves... (Ctrl+Shift+A) to pick parameters and (Stacked) the Y axis, then plot them. Grouped builds its lines automatically from the inputs.",
            "Right-click on any plot pane (Stacked) or on the Compact / Grouped surface for per-surface actions.",
            "File > Save Project (Ctrl+S) — save layout, curves, event lines, and annotations to a .DPX project file.",
            "File > Open Recent — re-open a recently saved DatPlotX project (.DPX) file.",
            "File > Export Image... (Ctrl+E) — export the current plot as an image. Works in all three modes.",
        ]),
        new SubHeadingBlock("Choosing a Plot Surface"),
        new ParagraphBlock(
            "All three surfaces share the same data import, image export, and hover-tooltip system. " +
            "They differ in how curves are laid out on screen and in which extra features each one " +
            "supports. The next three sections describe each surface in detail."),
        new BulletListBlock([
            "Stacked Panes — pick when you want to compare a handful of parameters with their own " +
            "Y-axis ranges in vertically stacked panels, or when you need annotations, intersection " +
            "callouts, or per-curve statistics. The familiar stripchart layout.",
            "Compact Plot Surface — pick when you want to fit many curves (often eight or more, " +
            "including booleans) onto a single page-sized exhibit. Modelled on dense FDA / FDM " +
            "(NTSB-style) flight data printouts.",
            "Grouped Parameter Plot — pick when each row of the file is one experimental point and " +
            "you want one line per unique combination of chosen input values (parametric performance " +
            "arrays, lookup tables, driftdown / climb grids). The sidebar picks the X and Y columns " +
            "and the grouping inputs.",
        ]),
        new SubHeadingBlock("Source Data Panel"),
        new ParagraphBlock(
            "The bottom panel shows the raw data table that was imported. Drag the splitter to resize, " +
            "or click the small arrow button on the splitter to collapse or expand the panel. " +
            "The collapsed state is saved in the project file."),
    ]);

    private static HelpSection StackedPanesSurface() => new("Stacked Panes Surface",
    [
        new ParagraphBlock(
            "Stacked Panes is the default plot surface: a vertical stack of independent panels, all " +
            "sharing a single X axis. Each pane can carry its own Y1 (left) and Y2 (right) axes, " +
            "title, gridlines, and per-curve statistics. Zoom or pan in any pane and the rest follow " +
            "via the shared X axis. The plot mode is chosen at project creation and locked for the " +
            "life of the project — switching modes requires New Project + re-import."),
        new SubHeadingBlock("When To Use"),
        new BulletListBlock([
            "Comparing a handful of parameters with very different Y ranges in their own panes.",
            "Workflows that need annotations (text, arrows) or intersection callouts.",
            "Per-curve statistics (18 of them, over named segments) shown alongside the plot.",
            "Reports built from individually formatted panes.",
        ]),
        new SubHeadingBlock("Adding Curves"),
        new BulletListBlock([
            "Curves > Add Curves... (Ctrl+Shift+A) — pick a parameter (the dropdown has a search " +
            "box for filtering long column lists), choose Y1 (left) or Y2 (right), then click Plot " +
            "Curve. The dialog stays open so you can plot multiple curves in sequence; click Close " +
            "(or press Esc) when finished.",
            "Right-click a specific pane > Add Curves to This Pane... — targets that pane directly " +
            "instead of the active pane.",
            "Curves > Manage Curves... (Ctrl+M) — list every curve across every pane and edit colour, " +
            "line style, marker style, Y-axis assignment, or visibility.",
            "Curves > Clear All Curves (Ctrl+Shift+Delete) — remove every curve (asks for confirmation).",
        ]),
        new SubHeadingBlock("Adding & Removing Panes"),
        new BulletListBlock([
            "Right-click a pane > Add Pane Below — insert a new empty pane directly under the clicked pane.",
            "Right-click a pane > Remove This Pane — delete the clicked pane (only available when more than one pane exists).",
            "Right-click a pane > Set Scale to Default — reset zoom and axis limits to fit all data in that pane.",
        ]),
        new SubHeadingBlock("Event Lines"),
        new BulletListBlock([
            "Event Lines > Add Event Line (Ctrl+L) — add a global event line at a chosen X value with a label.",
            "Right-click a pane > Add Event Line Here... — drop a line at the cursor X.",
            "Drag a line horizontally — the move propagates to every pane via the shared X axis.",
            "Right-click directly on a line — delete confirmation.",
            "Event Lines > Clear All Event Lines (Ctrl+Shift+L) — remove every event line (asks for confirmation).",
        ]),
        new SubHeadingBlock("Intersection Callouts & Statistics"),
        new ParagraphBlock(
            "Wherever an event line crosses a plotted curve, an intersection callout shows the Y " +
            "value at that crossing. Drag a callout to reposition it; positions persist with the " +
            "project. File > Export Intersections... (Ctrl+Shift+E) writes every current intersection " +
            "to CSV. For per-curve statistics, open the Statistics panel via " +
            "Statistics > Show Statistics Panel (Ctrl+R) — it shows a full statistic " +
            "set (Max / Min / Mean / StdDev / Slope by default, 18 available) for every " +
            "visible curve over a chosen segment. See the Statistics section " +
            "for segments, units, columns, and the inline overlay."),
        new SubHeadingBlock("Annotations"),
        new BulletListBlock([
            "Right-click a pane > Add Text Annotation Here... — place a free-floating text label at the cursor; drag to reposition.",
            "Right-click a pane > Add Arrow Annotation Here... — draw an arrow with the tip at the cursor; drag either end to adjust base or tip.",
            "Right-click an existing annotation > Edit Text / Edit Arrow / Delete — visible only when the right-click hits an annotation.",
        ]),
        new SubHeadingBlock("Format Pane Dialog"),
        new ParagraphBlock(
            "Right-click a pane > Format Pane... opens a per-pane formatting dialog with tabs for " +
            "Labels (title + axis labels + font), Grid (major / minor lines + appearance), X-Axis " +
            "(range, tick format, font), Y1-Axis and Y2-Axis (each with its own range + decimals), " +
            "Background (plot + data area colours), and Legend (legend font size). Use the " +
            "ColorPill swatches for colours and the Min / Max range inputs to set manual axis ranges."),
        new SubHeadingBlock("Right-Click Pane Menu"),
        new ParagraphBlock(
            "The pane right-click menu is the main way to drive per-pane actions. The full menu is:"),
        new BulletListBlock([
            "Add Curves to This Pane... / Manage Curve... / Clear This Pane",
            "Add Event Line Here... / Clear All Event Lines",
            "Add Text Annotation Here... / Add Arrow Annotation Here... (plus Edit / Delete when right-clicking on an existing annotation)",
            "Set Scale to Default / Add Pane Below / Remove This Pane",
            "Format Pane... / Export Image...",
        ]),
        new SubHeadingBlock("Hover Tooltip"),
        new ParagraphBlock(
            "When enabled (Tools > Show Hover Tooltips, Ctrl+T), hovering near a plotted point shows " +
            "the X value and the curve name with its Y value within a 20 px snap threshold. The tooltip " +
            "is suppressed while Ctrl is held so the crosshair / rubber-band zoom is unaffected. One " +
            "toggle drives all three surfaces."),
        new SubHeadingBlock("Pan & Zoom"),
        new TableBlock(
            "Mouse / Key                   Action\n" +
            "──────────────────────────────────────────\n" +
            "Scroll wheel / 2-finger swipe Zoom in / out at cursor\n" +
            "Ctrl + scroll                 Fine zoom (smaller step)\n" +
            "Left-click + drag             Pan the plot\n" +
            "Ctrl + left-drag              Rubber-band zoom to selected rectangle\n" +
            "Double-click / Home           Reset zoom to fit all data\n" +
            "Right-click on pane           Open the pane context menu\n" +
            "Right-click on event line     Confirm and delete the event line\n" +
            "Drag event line               Move horizontally; updates every pane"),
    ]);

    private static HelpSection CompactPlotSurface() => new("Compact Plot Surface",
    [
        new ParagraphBlock(
            "The Compact Plot Surface is a single plot area with one banded Y axis per curve, " +
            "alternating between the left and right edges. It is modelled on the dense FDA / FDM " +
            "(NTSB-style) flight data exhibits and is designed to fit many curves onto one page. " +
            "The plot mode is chosen at project creation and locked for the life of the project — " +
            "switching modes requires New Project + re-import."),
        new SubHeadingBlock("When To Use"),
        new BulletListBlock([
            "Fitting many curves (often eight or more, including booleans) onto a single page-sized exhibit.",
            "FDA / FDM / NTSB-style printouts where every parameter has its own narrow band.",
            "Boolean state channels (gear up/down, weight-on-wheels) that benefit from a narrower band.",
            "Compact supports text and arrow annotations, event-line intersection callouts, and the Statistics panel. The only feature it lacks versus Stacked is the inline corner overlay (the panel covers the same numbers).",
        ]),
        new SubHeadingBlock("Adding Curves"),
        new BulletListBlock([
            "Curves > Add Compact Curves... (Ctrl+Shift+A) — Stage 1 picks the CSV columns " +
            "(X column and non-numeric columns are filtered out); Stage 2 sets per-curve axis " +
            "side, color, line style, marker style, boolean band, and overflow. Boolean columns " +
            "(0/1, true/false, on/off, yes/no) are auto-detected and get a narrower band.",
            "Curves > Manage Curve... (Ctrl+M, or right-click the surface > Manage Curve...) — " +
            "pick a curve from the dropdown, then change color, line style, marker style, marker " +
            "color, visibility, marker size, per-curve Y-axis label font size, bold, and decimal " +
            "places. Click Delete to remove the selected curve.",
            "Curves > Clear All Curves (Ctrl+Shift+Delete) — remove every curve from the surface " +
            "(asks for confirmation).",
        ]),
        new SubHeadingBlock("Event Lines"),
        new BulletListBlock([
            "Right-click the surface > Add Event Line Here — drop a vertical line at the cursor X.",
            "Right-click directly on an existing line (within 6 px) > Delete Event Line.",
            "Drag a line horizontally to move it; movement is clamped to the plot area.",
            "Right-click > Clear All Event Lines — remove every line on the surface.",
            "Event lines persist with the project file, and where one crosses a curve a Compact " +
            "intersection callout shows the Y value. (The top-level Event Lines menu and the " +
            "intersection CSV export are Stacked-only.)",
        ]),
        new SubHeadingBlock("Format Pane Dialog"),
        new ParagraphBlock(
            "Tools > Format Pane... (Ctrl+F, or right-click the surface > Format Pane...) configures " +
            "the surface globally: grid (major / minor lines, minor line style), X-axis (label, " +
            "decimals, manual or auto-scale range, label font size + bold), and plot background " +
            "color. Per-curve formatting lives in Curves > Manage Curve... Settings are saved with " +
            "the project file."),
        new SubHeadingBlock("Right-Click Surface Menu"),
        new BulletListBlock([
            "Add Curves... — opens the Add Compact Curves dialog.",
            "Manage Curve... — opens the Manage Curve dialog.",
            "Clear All Curves — remove every curve (with confirmation).",
            "Add Event Line Here / Delete Event Line / Use as Segment Boundary / Clear All Event Lines — event-line controls. Use as Segment Boundary pairs two lines into a statistics segment.",
            "Reset View — auto-fit all axes to the data.",
            "Format Pane... — gridlines, X-axis label and range, background color.",
            "Export Image... — export the surface as PNG, JPEG, BMP, or SVG.",
        ]),
        new SubHeadingBlock("Statistics"),
        new ParagraphBlock(
            "The Compact surface supports the full Statistics panel: open it from " +
            "Statistics > Show Statistics Panel (Ctrl+R). Shift+drag across the " +
            "surface defines a segment (with a live preview band), and the active segment " +
            "is shaded across every curve band. See the Statistics section for details. " +
            "The inline corner overlay is Stacked-only."),
        new SubHeadingBlock("Hover Tooltip"),
        new ParagraphBlock(
            "When enabled (Tools > Show Hover Tooltips, Ctrl+T), moving the cursor over a curve " +
            "shows the curve label and the X / Y values at the cursor. One toggle drives both " +
            "Stacked and Compact surfaces."),
        new SubHeadingBlock("Pan & Zoom"),
        new ParagraphBlock(
            "Pan and zoom shortcuts mirror Stacked Panes. Hover over the X-axis labels to zoom only " +
            "X; hover over a band's Y axis to zoom only that band; hover over the plot interior to " +
            "zoom both at the cursor. macOS trackpad two-finger swipes are recognised as wheel " +
            "input."),
        new TableBlock(
            "Mouse / Key                   Action\n" +
            "──────────────────────────────────────────\n" +
            "Scroll wheel / 2-finger swipe Zoom in / out at cursor\n" +
            "Ctrl + scroll                 Fine zoom (smaller step)\n" +
            "Cmd + scroll (macOS)          Fine zoom (smaller step)\n" +
            "Left-click + drag             Pan the plot\n" +
            "Ctrl + left-drag              Rubber-band zoom to selected rectangle\n" +
            "Middle-click / Double-click   Reset zoom to fit all data\n" +
            "Home key                      Reset zoom to fit all data\n" +
            "Right-click                   Open the surface context menu"),
    ]);

    private static HelpSection GroupedParameterPlotSurface() => new("Grouped Parameter Plot Surface",
    [
        new ParagraphBlock(
            "The Grouped Parameter Plot draws one line per unique combination of selected input-" +
            "parameter values on a single surface. It is built for tabular / array-style data where " +
            "each row of the file is one experimental point — parametric performance arrays, lookup " +
            "tables, driftdown grids, climb-to-altitude tables. The plot mode is chosen at project " +
            "creation and locked for the life of the project — switching modes requires New Project " +
            "+ re-import."),
        new SubHeadingBlock("When To Use"),
        new BulletListBlock([
            "Flat \"fact table\" files where columns are inputs (e.g. weight, altitude, temperature) and outputs (e.g. fuel flow, climb rate), and each row is one measured / computed point.",
            "Comparing a family of curves: hold most inputs fixed and let one vary to fan out a line per value.",
            "Lookup-table and performance-array data, rather than continuous time series.",
        ]),
        new SubHeadingBlock("The Sidebar"),
        new ParagraphBlock(
            "Grouped mode replaces the top-bar X-Axis selector with a sidebar on the left of the " +
            "surface. The sidebar chooses the X and Y columns and lists each grouping input as a " +
            "dropdown of that column's distinct values. Drag the splitter to resize it."),
        new BulletListBlock([
            "X / Y columns — the axes of the plot. Y is the output you are plotting; X is what it is plotted against.",
            "Input dropdowns — one per grouping column. Pick a specific value to hold that input fixed, or pick \"All\" to fan out one line for every distinct value of that input.",
            "Choosing \"All\" on more than one input draws a line for every combination (the cartesian product). The number of lines is capped (48 by default) to keep the plot readable; distinct values per input are capped at 5000.",
            "Each input's distinct values are shown with the column's display label, unit suffix, and numeric format.",
        ]),
        new SubHeadingBlock("Configure Inputs Wizard"),
        new ParagraphBlock(
            "The Configure Inputs wizard chooses which columns are grouping inputs and which are the " +
            "X / Y columns. It opens automatically the first time you import into a Grouped project, " +
            "and you can reopen it any time from Tools > Configure Inputs... Changes rebuild the plot " +
            "and are saved with the project."),
        new SubHeadingBlock("Legend & Markers"),
        new BulletListBlock([
            "The legend identifies each line by its input combination; it can be shown or hidden.",
            "Markers at each data point can be shown or hidden — useful when points are sparse (one per row) rather than a dense trace.",
        ]),
        new SubHeadingBlock("Annotations"),
        new ParagraphBlock(
            "Grouped supports text and arrow annotations from the right-click surface menu (Add Text " +
            "Annotation / Add Arrow Annotation, plus Edit / Delete when the click hits one, and Clear " +
            "All Annotations). They are saved with the project. Grouped has no event lines, so it has " +
            "no intersection callouts, and statistics are not computed in Grouped mode (they add " +
            "little value for parametric data)."),
        new SubHeadingBlock("Image Export"),
        new ParagraphBlock(
            "File > Export Image... (Ctrl+E) and the right-click surface menu both export the Grouped " +
            "plot. The orientation dialog offers Landscape or Portrait."),
        new SubHeadingBlock("Hover Tooltip"),
        new ParagraphBlock(
            "With hover tooltips enabled (Tools > Show Hover Tooltips, Ctrl+T), moving the cursor near " +
            "a point shows that line's identity and the X / Y values at the cursor."),
        new SubHeadingBlock("Pan & Zoom"),
        new ParagraphBlock(
            "Pan and zoom mirror the other surfaces: scroll / two-finger swipe to zoom at the cursor, " +
            "left-drag to pan, Ctrl+left-drag to rubber-band zoom, and double-click or Home to reset " +
            "to fit all data."),
    ]);

    private static HelpSection FileMenu() => new("File Menu",
    [
        new ParagraphBlock(
            "The File menu provides all data import, project save/load, and export operations. " +
            "Every item works in all three plot modes (Export Intersections is Stacked-only)."),
        new TableBlock(
            "Action                   Shortcut         Description\n" +
            "──────────────────────────────────────────────────────────────────────\n" +
            "New Project              Ctrl+N           Pick plot surface, then load a CSV/TSV/X-Plane file\n" +
            "Open Project...          Ctrl+O           Open a saved DatPlotX (.DPX) project file\n" +
            "Open Recent                               Re-open a recent DatPlotX project file\n" +
            "Save Project             Ctrl+S           Save the current project in place\n" +
            "Save Project As...       Ctrl+Shift+S     Save the project to a new file path\n" +
            "Export Image...          Ctrl+E           Export the plot as PNG, JPEG, BMP, or SVG\n" +
            "Export Intersections...  Ctrl+Shift+E     (Stacked) Export event-line intersections to CSV\n" +
            "Exit                     Ctrl+Q           Quit the application"),
        new SubHeadingBlock("New Project Workflow"),
        new ParagraphBlock(
            "File > New Project (Ctrl+N) is the entry point for all data import. The plot-surface " +
            "picker appears first (Stacked Panes, Compact Plot Surface, or Grouped Parameter Plot — " +
            "the choice is locked for the life of the project; double-click a card to pick and create " +
            "in one step), followed by the file picker and the Import Options dialog. The plot " +
            "surface opens only after the file is accepted; cancelling at any step aborts the " +
            "new-project operation. For a Grouped project, the Configure Inputs wizard then opens so " +
            "you can pick the X / Y columns and grouping inputs."),
        new SubHeadingBlock("Project Files"),
        new ParagraphBlock(
            "A DatPlotX project (.DPX) stores the plot mode; the pane layout (Stacked), compact " +
            "curves (Compact), or grouped configuration (Grouped); the X-axis parameter; event lines " +
            "and intersection callouts; text and arrow annotations (all modes); the chosen statistics " +
            "columns and segments; and a reference to the source data file. The on-disk format is " +
            "GZip-compressed JSON. Re-opening a project re-imports the data file and rebuilds the plot."),
        new SubHeadingBlock("Image Export"),
        new ParagraphBlock(
            "Export Image works in all three modes. The orientation dialog asks for Landscape " +
            "(default) or Portrait. Each surface also offers Export Image... from its right-click " +
            "menu (per-pane in Stacked, the surface menu in Compact and Grouped). Stacked and Grouped " +
            "export to PNG, JPEG, or BMP; Compact exports to PNG, JPEG, or SVG."),
        new SubHeadingBlock("Intersection Export (Stacked)"),
        new ParagraphBlock(
            "Export Intersections... (Ctrl+Shift+E) is enabled in Stacked Panes mode and writes every " +
            "current event-line / curve intersection (X, curve name, Y value) to a CSV file. Compact " +
            "mode shows intersection callouts on the surface too, but the CSV export of them is " +
            "Stacked-only."),
    ]);

    private static HelpSection CurvesMenu() => new("Curves Menu",
    [
        new ParagraphBlock(
            "The Curves menu adds, manages, and clears curves. The exact items shown depend on " +
            "the active plot mode — Stacked Panes uses the per-pane Add Curves / Manage Curves " +
            "dialogs; Compact uses the two-stage Add Compact Curves dialog and a per-curve Manage " +
            "Curve dialog. The Curves menu is hidden in Grouped Parameter Plot mode, where lines are " +
            "generated from the sidebar inputs rather than added one at a time — see Tools > " +
            "Configure Inputs..."),
        new TableBlock(
            "Action                    Shortcut             Mode      Description\n" +
            "──────────────────────────────────────────────────────────────────────\n" +
            "Add Curves...             Ctrl+Shift+A         Stacked   Open the Add Curves dialog\n" +
            "Add Compact Curves...     Ctrl+Shift+A         Compact   Open the Compact two-stage dialog\n" +
            "Manage Curves...          Ctrl+M               Stacked   Edit, show/hide, or remove curves\n" +
            "Manage Curve...           Ctrl+M               Compact   Edit one curve or delete it\n" +
            "Clear All Curves          Ctrl+Shift+Delete    Both      Remove every curve (asks for confirmation)"),
        new SubHeadingBlock("Add Curves Dialog (Stacked)"),
        new BulletListBlock([
            "Select a Parameter to plot — the dropdown has a search box for filtering long column lists.",
            "Choose Y-Axis: Y1 (left) or Y2 (right).",
            "Click Plot Curve to immediately add the curve to the active pane.",
            "The dialog stays open so you can plot multiple curves in sequence.",
            "Click Close (or press Esc) when finished.",
            "To plot directly into a specific pane, use the right-click menu on that pane instead.",
        ]),
        new SubHeadingBlock("Manage Curves Dialog (Stacked)"),
        new BulletListBlock([
            "Lists every curve currently plotted across all panes in a single table.",
            "Toggle visibility per curve without deleting it.",
            "Edit colour, line style, marker style, and Y axis assignment.",
            "Remove individual curves.",
        ]),
        new SubHeadingBlock("Add Compact Curves Dialog (Compact)"),
        new BulletListBlock([
            "Two-stage flow: Stage 1 picks the columns to plot (X column and non-numeric columns are filtered out, with a search box).",
            "Stage 2 sets per-curve axis side (left / right), color, line style, marker style, boolean band, and overflow.",
            "Boolean columns (0/1, true/false, on/off, yes/no) are auto-detected and get a narrower band.",
            "Color and axis side cycle from the existing curve count so new additions don't all collide on the left.",
        ]),
        new SubHeadingBlock("Manage Curve Dialog (Compact)"),
        new BulletListBlock([
            "Pick a single curve from the dropdown to edit.",
            "Change color, line style, marker style, marker color (decouples line and marker colours when set), and visibility.",
            "Adjust marker size, per-curve Y-axis label font size, bold, and decimal places.",
            "Click Delete to remove the selected curve, or Apply / Cancel for the rest of the edits.",
        ]),
        new SubHeadingBlock("Clear All Curves"),
        new ParagraphBlock(
            "Clear All Curves asks for confirmation in both modes before removing every curve. " +
            "The count of curves about to be removed is shown so you can back out safely."),
        new ParagraphBlock(
            "Importing data does not auto-plot anything in either mode — you choose which parameters " +
            "become curves."),
    ]);

    private static HelpSection EventLinesMenu() => new("Event Lines Menu",
    [
        new ParagraphBlock(
            "Event lines are vertical reference lines drawn at a specific X value. Use them to mark " +
            "significant points in the data — engine start, takeoff, an alert threshold — and read " +
            "the Y values where they cross each curve."),
        new SubHeadingBlock("Where The Controls Live"),
        new BulletListBlock([
            "Stacked Panes — top-level Event Lines menu (Add Event Line, Clear All Event Lines) plus right-click pane menu (Add Event Line Here).",
            "Compact Plot Surface — right-click surface menu only (Add Event Line Here, Delete Event Line, Clear All Event Lines). The top-level Event Lines menu is hidden in Compact mode.",
        ]),
        new TableBlock(
            "Action                  Shortcut          Mode      Description\n" +
            "──────────────────────────────────────────────────────────────────────\n" +
            "Add Event Line          Ctrl+L            Stacked   Add a global event line at a chosen X value\n" +
            "Clear All Event Lines   Ctrl+Shift+L      Stacked   Remove every event line (asks for confirmation)"),
        new SubHeadingBlock("Adding Event Lines (Stacked)"),
        new BulletListBlock([
            "Use Event Lines > Add Event Line (Ctrl+L) to add a line at a specific X value with a label.",
            "Or right-click on a pane and choose Add Event Line Here... to add it at the cursor X.",
            "Drag a line horizontally to move it; the move propagates to all panes.",
            "Right-click directly on a line for delete confirmation.",
        ]),
        new SubHeadingBlock("Adding Event Lines (Compact)"),
        new BulletListBlock([
            "Right-click the Compact surface and choose Add Event Line Here to drop a line at the cursor X.",
            "Right-click directly on an existing line (within 6 px) to access Delete Event Line.",
            "Drag a line horizontally to move it; movement is clamped to the plot area.",
            "Clear All Event Lines is also available from the right-click menu.",
        ]),
        new SubHeadingBlock("Intersection Callouts"),
        new ParagraphBlock(
            "When an event line crosses a curve, the intersection Y value is computed automatically " +
            "and shown as a callout — in both Stacked Panes and Compact Plot Surface modes. Drag a " +
            "callout to reposition it; positions are saved with the project. File > Export " +
            "Intersections... (Ctrl+Shift+E) writes all current event-line intersections to a CSV " +
            "file; that CSV export is Stacked-only."),
    ]);

    private static HelpSection ToolsMenu() => new("Tools Menu",
    [
        new ParagraphBlock(
            "The Tools menu hosts session toggles, the Format Pane dialog for the Compact surface, " +
            "and Settings. Stacked-mode pane formatting is reached from the per-pane right-click " +
            "menu since each pane is formatted independently."),
        new TableBlock(
            "Item                   Shortcut    Mode      Description\n" +
            "──────────────────────────────────────────────────────────────────────\n" +
            "Show Hover Tooltips    Ctrl+T      All       Toggle the hover tooltip on every surface\n" +
            "Format Pane...         Ctrl+F      Compact   Configure grid, X-axis, and background\n" +
            "Configure Inputs...                Grouped   Pick grouping inputs and the X / Y columns\n" +
            "Settings...            Ctrl+,      All       Persistent preferences (e.g. default tooltip state)"),
        new SubHeadingBlock("Hover Tooltips"),
        new ParagraphBlock(
            "When enabled, hovering near a plotted point shows the X value and the curve name with " +
            "its Y value. The Stacked-mode tooltip is suppressed while Ctrl is held so the crosshair " +
            "/ rubber-band zoom is unaffected. The toggle in the Tools menu changes the state for " +
            "the current session; the Settings dialog persists the default on/off state across " +
            "launches. One toggle drives all three surfaces."),
        new SubHeadingBlock("Format Pane (Compact)"),
        new ParagraphBlock(
            "Tools > Format Pane... (Ctrl+F) is enabled only in Compact mode. It configures the " +
            "surface globally: grid (major / minor lines, minor line style), X-axis (label, decimals, " +
            "manual or auto-scale range, label font size + bold), and plot background color. Per-curve " +
            "formatting lives in Curves > Manage Curve..."),
        new SubHeadingBlock("Format Pane (Stacked)"),
        new ParagraphBlock(
            "Stacked-mode pane formatting is accessed from the per-pane right-click menu (Format " +
            "Pane...) since each pane has independent formatting. The dialog has per-axis tabs: " +
            "Labels (title + axis labels + font), Grid (major / minor lines + appearance), X-Axis " +
            "(range, tick format, font), Y1-Axis and Y2-Axis (each with own range + decimals), " +
            "Background (plot + data area colours), and Legend (legend + stats font sizes)."),
        new SubHeadingBlock("Configure Inputs (Grouped)"),
        new ParagraphBlock(
            "Tools > Configure Inputs... is shown only in Grouped Parameter Plot mode. It opens the " +
            "wizard that chooses which columns are grouping inputs and which are the X / Y columns. " +
            "It also opens automatically the first time you import into a Grouped project. Changes " +
            "rebuild the plot and are saved with the project."),
    ]);

    private static HelpSection HelpMenu() => new("Help Menu",
    [
        new ParagraphBlock(
            "The Help menu opens this user guide, the What's New release notes, and the About dialog."),
        new TableBlock(
            "Item            Shortcut    Description\n" +
            "──────────────────────────────────────────────────────────────────────\n" +
            "User Guide      F1          Open this user guide window\n" +
            "What's New      Shift+F1    Browse the release notes for every version\n" +
            "About DatPlotX              Show the version number and copyright"),
    ]);

    private static HelpSection SourceDataPanel() => new("Source Data Panel",
    [
        new ParagraphBlock(
            "The panel at the bottom of the window shows the raw data from the imported file. " +
            "It is a read-only preview of the underlying table, available in all three plot modes."),
        new BulletListBlock([
            "Drag the horizontal splitter above the panel to resize.",
            "Click the small arrow button on the splitter to collapse the panel; click again to expand. The collapsed state is saved with the project file.",
            "Columns can be resized and sorted; sorting affects the preview only, not the plotted data.",
            "Very large files are previewed up to a row cap (the status bar reports if rows are truncated). All rows are still used for plotting.",
        ]),
    ]);

    private static HelpSection XAxisParameter() => new("X-Axis Parameter",
    [
        new ParagraphBlock(
            "The X-Axis Parameter selector at the top right of the window picks which column from " +
            "the imported file is used as the X axis. The selection is saved with the project. It is " +
            "used by the Stacked Panes and Compact Plot Surface modes; in Grouped Parameter Plot the " +
            "top-bar selector is hidden because the sidebar owns the X (and Y) column choice instead."),
        new BulletListBlock([
            "Stacked Panes — every pane shares this X axis; changing the X parameter rebuilds every curve and re-synchronises pan / zoom across panes.",
            "Compact Plot Surface — every band shares this X axis; changing the X parameter rebuilds the entire surface.",
            "Grouped Parameter Plot — the X and Y columns are chosen in the sidebar (or the Configure Inputs wizard), not from this top-bar selector.",
            "Choose a monotonic column (typically time or distance) for the most useful Stacked / Compact plots.",
            "If a saved project's X column no longer exists in the imported data, the selection is cleared so it cannot carry stale state.",
        ]),
    ]);

    private static HelpSection AnnotationsAndCallouts() => new("Annotations & Callouts",
    [
        new ParagraphBlock(
            "Text and arrow annotations are available in all three plot modes — Stacked Panes, " +
            "Compact Plot Surface, and Grouped Parameter Plot. Event-line intersection callouts " +
            "are available wherever there are event lines (Stacked and Compact); Grouped has no " +
            "event lines, so it has no callouts. In every case you add and edit these from the " +
            "right-click menu on the pane or surface; all of them are saved in the .DPX project."),
        new SubHeadingBlock("Text Annotations (All Modes)"),
        new BulletListBlock([
            "Right-click a pane (Stacked) or the surface (Compact / Grouped) and choose Add Text Annotation Here... to place a label at the cursor.",
            "Drag the text to reposition it.",
            "Right-click an existing annotation for Edit Text... or Delete.",
            "On the Compact surface, a text annotation is anchored to a curve's banded Y axis, so it stays with that band's data coordinates.",
        ]),
        new SubHeadingBlock("Arrow Annotations (All Modes)"),
        new BulletListBlock([
            "Right-click a pane or surface and choose Add Arrow Annotation Here... — the arrow tip is placed at the cursor.",
            "Drag either end to adjust base or tip independently; the label rotates with the arrow.",
            "Right-click an existing arrow for Edit Arrow... or Delete.",
        ]),
        new SubHeadingBlock("Intersection Callouts (Stacked & Compact)"),
        new ParagraphBlock(
            "Wherever an event line crosses a plotted curve, an intersection callout shows the Y " +
            "value at that crossing, and re-interpolates live as you drag the event line. Drag a " +
            "callout to reposition it; positions are saved with the project. File > Export " +
            "Intersections... (Ctrl+Shift+E) exports every current intersection to CSV in Stacked " +
            "mode. Grouped Parameter Plot has no event lines, so no callouts."),
    ]);

    private static HelpSection AnalysisAndStatistics() => new("Statistics",
    [
        new ParagraphBlock(
            "The Statistics panel computes per-curve statistics over a chosen X-range and " +
            "shows them in a sortable, resizable table in the bottom pane, side-by-side with the " +
            "Source Data table. It replaces the old fixed " +
            "Min / Mean / Max box with a full statistic set, named segments, and an optional " +
            "inline overlay. Statistics work in both Stacked Panes and Compact Plot Surface modes; " +
            "the inline corner overlay is Stacked-only for now (Compact uses the panel only). " +
            "They are not available in Grouped Parameter Plot mode."),
        new ParagraphBlock(
            "Everything lives under one menu: Statistics > Show Statistics Panel, Inline Overlay, " +
            "Segments..., Columns..., and Tolerance Band... The menu appears only in modes that " +
            "support statistics (Stacked and Compact)."),
        new SubHeadingBlock("Opening The Panel"),
        new BulletListBlock([
            "Statistics > Show Statistics Panel (Ctrl+R) — toggles the panel in the bottom pane, beside the Source Data table, with a draggable splitter between them. Toggling it on expands the bottom pane if collapsed. The panel is hidden by default and its open/closed state is per-session (not saved in the project).",
            "The panel shows one row per visible curve and one column per chosen statistic. Columns are individually resizable; drag the splitter between headers.",
            "Copy as TSV / Copy as Markdown — copy the whole table to the clipboard for pasting into a spreadsheet or a report. Export CSV writes the table (and any tolerance bands) to a file.",
        ]),
        new SubHeadingBlock("Statistics (Columns)"),
        new ParagraphBlock(
            "Eighteen statistics are available, grouped by category. The default columns are Max, Min, " +
            "Mean, StdDev, and Slope. Choose which appear, and in what order, from " +
            "Statistics > Columns... The choice is saved in the project file. The full set:"),
        new BulletListBlock([
            "Basic — Max, Min, Mean, Median, Range, Peak-to-Peak, Count, NaN Count.",
            "Dispersion — Standard Deviation, Variance, RMS, percentiles (P5 / P50 / P95).",
            "Temporal — Slope (linear-regression fit; hover the Slope cell to see R² and the intercept, and it shows an engineer-friendly derived rate such as ft/min or kt when the curve and X units are known), Integral (trapezoidal area), First, Last.",
            "Max / Min are point-on-curve statistics: each cell carries a target (⊕) button that flashes the exact (X, Y) location on the plot.",
        ]),
        new ParagraphBlock(
            "Cells skip NaN samples. A cell shows \"—\" when the statistic has no value for the " +
            "current segment (e.g. an empty window or a flat line with no slope). Numbers carry the " +
            "curve's unit when one is known."),
        new SubHeadingBlock("Curve Units"),
        new ParagraphBlock(
            "Units drive the derived-rate labels (e.g. a ft curve against an s X-axis reports slope " +
            "as ft/min). Set or edit a curve's unit in the Add Curves and Format Curve dialogs — the " +
            "unit field auto-fills from a \"Header (Unit)\" column name on import. Units round-trip in " +
            "the project file."),
        new SubHeadingBlock("Segments"),
        new ParagraphBlock(
            "A segment is the X-range the statistics are computed over. The picker at the top of the " +
            "panel chooses the active segment; the active segment is drawn as a shaded band on the " +
            "plot. Segment kinds:"),
        new BulletListBlock([
            "Visible window (default) — tracks the current zoom / pan. Statistics update live as you move the X-axis.",
            "Full data — the entire X-range of the loaded data, regardless of zoom.",
            "Manual (Shift+drag) — hold Shift and drag horizontally across a pane (Stacked) or the surface (Compact) to define a fixed [X1, X2] segment. A live preview band follows the cursor as you drag; on release the segment is added to the picker and made active immediately.",
            "Event-line pair — right-click an event line > Use as Segment Boundary, then right-click a second event line. The segment spans the two lines and tracks them live: move a boundary line and the segment (and its stored range) follow. Works with both Stacked and Compact event lines.",
        ]),
        new SubHeadingBlock("Segments Dialog"),
        new BulletListBlock([
            "Open from Statistics > Segments... (or the segment picker's manage entry). Lists every segment with its range.",
            "Rename or delete user-defined segments; the implicit Visible-window segment cannot be renamed or removed.",
            "Set any segment active, or jump back to the Visible-window segment.",
            "User-defined segments and the active selection are saved in the .DPX project file and restored on load, along with your column choice. (The panel's open/closed state stays session-only.)",
        ]),
        new SubHeadingBlock("Inline Overlay"),
        new ParagraphBlock(
            "Statistics > Inline Overlay (Ctrl+I) draws the top statistics for each visible curve in " +
            "the corner of its pane, so you can read key numbers without opening the side panel. " +
            "Off by default; the toggle is per-session. It honours the active segment, so the inline " +
            "numbers match the panel. This overlay is a Stacked Panes feature — in Compact mode the " +
            "menu item is disabled and statistics are shown in the side panel only."),
        new SubHeadingBlock("Tolerance Bands"),
        new ParagraphBlock(
            "Statistics > Tolerance Band... defines a ± tolerance / spec-limit band on a curve and " +
            "reports % in-band, boundary crossings, exceedance duration, and the maximum excursion " +
            "in a dedicated section below the statistics table."),
        new SubHeadingBlock("Performance"),
        new ParagraphBlock(
            "Recompute while panning or zooming is debounced — a continuous drag triggers a single " +
            "recompute when the axis settles, so large datasets stay responsive. The table updates " +
            "its values in place rather than rebuilding when only the window shifts."),
    ]);

    private static HelpSection ImportOptions() => new("Import Options Dialog",
    [
        new ParagraphBlock(
            "When importing a data file (Stacked or Compact, the import flow is identical), the " +
            "Import Options dialog confirms how the file should be parsed and previews the first " +
            "100 lines so you can verify the format before committing."),
        new SubHeadingBlock("Format"),
        new BulletListBlock([
            "Delimiter — auto-detect, comma, semicolon, tab, pipe, or space.",
            "Decimal separator — dot (\".\") or comma (\",\"). Determines the culture used to parse numeric values.",
        ]),
        new SubHeadingBlock("Lines"),
        new BulletListBlock([
            "Header line — the 1-based line number that contains column names (use 0 if no header row).",
            "Unit line — optional 1-based line for unit strings (use 0 if not present).",
            "Data starts — the 1-based line of the first numeric data row.",
        ]),
        new ParagraphBlock(
            "CSV comment lines starting with # are skipped automatically. The X-Plane format is " +
            "auto-detected by its own header marker and uses a separate parser."),
    ]);

    private static HelpSection KeyboardShortcuts() => new("Keyboard Shortcuts",
    [
        new ParagraphBlock(
            "On macOS, Ctrl maps to the Command key for these shortcuts. Press Esc inside any dialog " +
            "to dismiss it (equivalent to Cancel). Where a shortcut behaves differently per mode, " +
            "the table calls out which surface (Stacked / Compact / Grouped) it applies to."),
        new SubHeadingBlock("File (All Modes)"),
        new TableBlock(
            "Action                        Shortcut\n" +
            "──────────────────────────────────────────\n" +
            "New Project                   Ctrl+N\n" +
            "Open Project...               Ctrl+O\n" +
            "Save Project                  Ctrl+S\n" +
            "Save Project As...            Ctrl+Shift+S\n" +
            "Export Image...               Ctrl+E\n" +
            "Export Intersections...       Ctrl+Shift+E    (Stacked)\n" +
            "Exit                          Ctrl+Q"),
        new SubHeadingBlock("Curves"),
        new TableBlock(
            "Action                        Shortcut          Mode\n" +
            "──────────────────────────────────────────\n" +
            "Add Curves...                 Ctrl+Shift+A      Stacked\n" +
            "Add Compact Curves...         Ctrl+Shift+A      Compact\n" +
            "Manage Curves...              Ctrl+M            Stacked\n" +
            "Manage Curve...               Ctrl+M            Compact\n" +
            "Clear All Curves              Ctrl+Shift+Delete Both"),
        new SubHeadingBlock("Event Lines (Stacked Only)"),
        new TableBlock(
            "Action                        Shortcut\n" +
            "──────────────────────────────────────────\n" +
            "Add Event Line                Ctrl+L\n" +
            "Clear All Event Lines         Ctrl+Shift+L"),
        new ParagraphBlock(
            "Compact-mode event lines are driven from the right-click surface menu — there are no " +
            "keyboard shortcuts."),
        new SubHeadingBlock("Tools"),
        new TableBlock(
            "Action                        Shortcut          Mode\n" +
            "──────────────────────────────────────────\n" +
            "Toggle Hover Tooltips         Ctrl+T            All\n" +
            "Format Pane... (Compact)      Ctrl+F            Compact\n" +
            "Settings...                   Ctrl+,            All"),
        new ParagraphBlock(
            "Stacked-mode Format Pane is reached from the per-pane right-click menu, not a keyboard " +
            "shortcut, since each pane is formatted independently."),
        new SubHeadingBlock("Statistics"),
        new TableBlock(
            "Action                        Shortcut          Mode\n" +
            "──────────────────────────────────────────\n" +
            "Show Statistics Panel         Ctrl+R            Stacked / Compact\n" +
            "Inline Overlay                Ctrl+I            Stacked"),
        new SubHeadingBlock("Help (All Modes)"),
        new TableBlock(
            "Action                        Shortcut\n" +
            "──────────────────────────────────────────\n" +
            "User Guide                    F1\n" +
            "What's New                    Shift+F1"),
        new SubHeadingBlock("Plot Interaction (All Modes)"),
        new TableBlock(
            "Mouse / Key                   Action\n" +
            "──────────────────────────────────────────\n" +
            "Scroll wheel / 2-finger swipe Zoom in / out at cursor\n" +
            "Ctrl + scroll                 Fine zoom (smaller step)\n" +
            "Cmd + scroll (macOS)          Fine zoom (smaller step)\n" +
            "Left-click + drag             Pan the plot\n" +
            "Ctrl + left-drag              Rubber-band zoom to selected rectangle\n" +
            "Double-click / Home           Reset zoom to fit all data\n" +
            "Middle-click (Compact)        Reset zoom to fit all data\n" +
            "Right-click on pane/surface   Open the context menu\n" +
            "Right-click on event line     Confirm and delete the event line\n" +
            "Drag event line               Move horizontally; updates every pane (Stacked) / clamped to plot area (Compact)"),
        new SubHeadingBlock("Dialogs (All Modes)"),
        new TableBlock(
            "Key                           Action\n" +
            "──────────────────────────────────────────\n" +
            "Esc                           Close the active dialog (Cancel)\n" +
            "Enter                         Accept the default action where applicable"),
    ]);
}
