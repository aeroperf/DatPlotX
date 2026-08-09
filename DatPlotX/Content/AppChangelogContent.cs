// Application version history. Update this file when releasing new versions.

namespace DatPlotX.Content;

public static class AppChangelogContent
{
    public static ChangelogEntry[] GetEntries() =>
    [
        new("1.2.0", "9 August 2026",
        [
            "Event lines now show their X-axis value on a second row under the label, in both Stacked and Compact plot modes.",
            "The value follows the line as you drag it, so it always matches where the line sits.",
            "New \"Event Line\" tab in Format Pane (right-click the plot) turns the X value on or off and sets its decimal places — 2 by default.",
            "In Stacked mode the setting is per pane; in Compact mode it applies to the whole surface. Either way it is saved with the project.",
        ]),
        new("1.1.0", "18 July 2026",
        [
            "Project files now save atomically — a crash or full disk mid-save can no longer corrupt an existing .DPX.",
            "Closing the app from the window controls (or ⌘Q) now prompts to save unsaved changes.",
            "Hover tooltips, box-zoom, and pan now behave correctly on panes with both a left and right Y axis.",
            "One-sided manual axis ranges are honored, and inverted (min ≥ max) ranges are rejected with a message.",
            "Compact-mode statistics sort by X first, fixing wrong Mean / Integral / tolerance-band results on unsorted data.",
            "Metrics now skip ±Infinity samples; StdDev and Variance are labeled \"(pop)\" to show they are population statistics.",
            "Import: comma delimiter + comma decimal is now blocked; number-column detection matches the parser so values like (1.5) no longer vanish.",
            "Multi-pane SVG export now produces real vector SVG; tab and report exports use invariant number formatting.",
            "Curve Manager Cancel discards edits correctly; arrow, segment, and Settings dialogs fixed.",
            "Grouped plot: \"Set Scale to Default\" no longer duplicates annotations.",
            "macOS: keyboard shortcuts use ⌘ (Command) and the menus show the ⌘ symbol.",
        ]),
        new("1.0.0", "6 July 2026",
        [
            "Initial public release of DatPlotX.",
        ]),
    ];
}
