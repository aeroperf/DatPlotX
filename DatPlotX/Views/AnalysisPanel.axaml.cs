using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DatPlotX.Models.Analysis;
using DatPlotX.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

namespace DatPlotX.Views;

/// <summary>
/// Dockable Analysis Results panel. Avalonia's <see cref="DataGrid"/> doesn't deal cleanly
/// with the dynamic-column scenario (one column per enabled metric, churn on metric toggle),
/// so the table is laid out manually into a <see cref="Grid"/> — same pattern the source-data
/// panel uses (<see cref="MainWindow.RebuildDataGridColumns"/>).
/// </summary>
public partial class AnalysisPanel : UserControl
{
    private AnalysisPanelViewModel? _vm;

    public AnalysisPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ── Token brushes resolved from DpxTokens.axaml (app-light palette). Falling back to a
    //    neutral default keeps the designer (which may lack merged dictionaries) from crashing. ──
    private IBrush Brush(string key, Color fallback) =>
        this.TryFindResource(key, out var r) && r is IBrush b ? b : new SolidColorBrush(fallback);

    private IBrush Sunken => Brush("DpxSunken", Color.Parse("#F0F0EE"));
    private IBrush Raised => Brush("DpxRaised", Color.Parse("#F5F5F4"));
    private IBrush Divider => Brush("DpxDivider", Color.Parse("#ECECE9"));
    private IBrush TextMain => Brush("DpxText", Color.Parse("#18181B"));
    private IBrush TextSubtle => Brush("DpxText3", Color.Parse("#8E8E93"));

    private FontFamily MonoFamily =>
        this.TryFindResource("DpxMonoFamily", out var r) && r is FontFamily f ? f : new FontFamily("Consolas, Menlo, Monaco, monospace");

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.Rows.CollectionChanged -= OnRowsChanged;
            _vm.Columns.CollectionChanged -= OnColumnsChanged;
            _vm.TableInvalidated -= OnTableInvalidated;
            _vm.BandsInvalidated -= OnBandsInvalidated;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.ClipboardRequested -= OnClipboardRequested;
        }
        _vm = DataContext as AnalysisPanelViewModel;
        if (_vm is not null)
        {
            _vm.Rows.CollectionChanged += OnRowsChanged;
            _vm.Columns.CollectionChanged += OnColumnsChanged;
            _vm.TableInvalidated += OnTableInvalidated;
            _vm.BandsInvalidated += OnBandsInvalidated;
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.ClipboardRequested += OnClipboardRequested;
            RebuildTable();
            RebuildBands();
        }
    }

    // Results updates raise TableInvalidated exactly once; rebuild from that. Per-row collection
    // events are suppressed during the VM's bulk row swap so we don't rebuild once per row.
    private void OnTableInvalidated(object? sender, EventArgs e) => RebuildTable();
    private void OnBandsInvalidated(object? sender, EventArgs e) => RebuildBands();
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm is { SuppressRowEvents: true }) return;
        RebuildTable();
    }
    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildTable();
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e) { /* IsBusy etc handled by binding */ }

    private void OnDeleteSegmentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: AnalysisSegmentChoice choice } && _vm is not null)
        {
            _vm.DeleteSegmentCommand.Execute(choice);
        }
    }

    private async void OnClipboardRequested(object? sender, string text)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    /// <summary>
    /// Rebuild the entire table — header row + body rows. Cheap (rows = curves, columns =
    /// ~5–10 metrics) so just blow it away each time the ViewModel updates.
    /// </summary>
    private void RebuildTable()
    {
        if (_vm is null) return;

        var grid = this.FindControl<Grid>("ResultsGrid");
        if (grid is null) return;

        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        // Layout: each data column is Auto (defaults to the width of its widest cell), with a
        // draggable GridSplitter column sandwiched between data columns so the user can resize.
        // Data columns therefore live at even grid indices (0, 2, 4, …); splitters at odd indices.
        int totalRows = _vm.Rows.Count + 1; // +1 for header
        int dataColCount = _vm.Columns.Count + 1; // +1 for the Curve name column

        for (int dc = 0; dc < dataColCount; dc++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (dc < dataColCount - 1)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        // Trailing spacer column: a fixed gutter after the last metric column so its action
        // buttons (e.g. the Slope ╱) never sit flush against the card's clip edge and get cut off.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });

        // Header row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddHeaderCell(grid, "Curve", 0, 0);
        for (int c = 0; c < _vm.Columns.Count; c++)
            AddHeaderCell(grid, _vm.Columns[c].DisplayName, 0, DataToGridColumn(c + 1));

        // Splitters span every row so the drag handle covers the full table height.
        for (int dc = 0; dc < dataColCount - 1; dc++)
            AddColumnSplitter(grid, DataToGridColumn(dc) + 1, totalRows);

        int totalGridCols = grid.ColumnDefinitions.Count;

        // Body rows
        for (int r = 0; r < _vm.Rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = _vm.Rows[r];
            int gridRow = r + 1;

            // Zebra band behind the whole row (spans all columns, sits behind cell content).
            // Even rows get a raised wash; a bottom divider separates rows. Hover → accent wash
            // via the Border.dpx-rowband:pointerover style.
            var band = new Border
            {
                Background = (r % 2 == 1) ? Raised : Brushes.Transparent,
                BorderBrush = Divider,
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            band.Classes.Add("dpx-rowband");
            Grid.SetRow(band, gridRow);
            Grid.SetColumn(band, 0);
            Grid.SetColumnSpan(band, totalGridCols);
            grid.Children.Add(band);

            // Curve name + color swatch
            var namePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(10, 7),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            namePanel.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(3),
                Background = SafeColor(row.ColorHex),
                BoxShadow = BoxShadows.Parse("inset 0 0 0 0.5 #33000000"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            namePanel.Children.Add(new TextBlock
            {
                Text = row.DisplayName,
                Foreground = TextMain,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
            });
            Grid.SetRow(namePanel, gridRow);
            Grid.SetColumn(namePanel, 0);
            grid.Children.Add(namePanel);

            // Metric cells
            for (int c = 0; c < _vm.Columns.Count; c++)
            {
                var col = _vm.Columns[c];
                row.Cells.TryGetValue(col.MetricId, out var cell);
                var cellEl = BuildCell(row, col, cell);
                Grid.SetRow(cellEl, gridRow);
                Grid.SetColumn(cellEl, DataToGridColumn(c + 1));
                grid.Children.Add(cellEl);
            }
        }
    }

    /// <summary>Map a logical data-column index to its grid-column index (splitters occupy the odd slots).</summary>
    private static int DataToGridColumn(int dataIndex) => dataIndex * 2;

    private static void AddColumnSplitter(Grid grid, int col, int rowSpan)
    {
        var splitter = new GridSplitter
        {
            Width = 5,
            Background = Brushes.Transparent,
            ResizeBehavior = GridResizeBehavior.PreviousAndCurrent,
            ResizeDirection = GridResizeDirection.Columns,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetColumn(splitter, col);
        Grid.SetRow(splitter, 0);
        Grid.SetRowSpan(splitter, rowSpan);
        grid.Children.Add(splitter);
    }

    private void AddHeaderCell(Grid grid, string text, int row, int col)
    {
        var border = new Border
        {
            Background = Sunken,
            BorderBrush = Divider,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 7),
            Child = new TextBlock
            {
                Text = text.ToUpperInvariant(),
                FontWeight = FontWeight.SemiBold,
                FontSize = 10.5,
                Foreground = TextSubtle,
                LetterSpacing = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
            }
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        grid.Children.Add(border);
    }

    private Grid BuildCell(AnalysisRowViewModel row, AnalysisColumnViewModel col, AnalysisCellViewModel? cell)
    {
        // Value (col 0) + action-button cluster (col 1). Both columns are Auto so the cell hugs its
        // content and the buttons are always inside the measured/scrollable width — a "*" value
        // column here measures to infinite width inside the Auto outer grid column and pushed the
        // button cluster off the right edge (unreachable even when scrolled fully right). Decimal
        // line-up is instead achieved by giving the value TextBlock a shared MinWidth + right-align.
        var panel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            Margin = new Thickness(10, 6),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (cell is null || double.IsNaN(cell.Value))
        {
            var dash = new TextBlock
            {
                Text = "—",
                Foreground = TextSubtle,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
            };
            Grid.SetColumn(dash, 0);
            panel.Children.Add(dash);
            return panel;
        }

        var value = new TextBlock
        {
            Text = cell.DisplayText(row.Unit),
            FontFamily = MonoFamily,
            FontSize = 12,
            Foreground = TextMain,
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 72,   // shared width so decimals line up and the ╱ button aligns down the column
                             // even for wider 6-decimal values (e.g. -0.000197)
            Margin = new Thickness(0, 0, 6, 0),
        };
        // Surface a metric's secondary scalars (slope's R² / intercept) on hover.
        if (cell.Tooltip(row.Unit) is { } extrasTip)
            ToolTip.SetTip(value, extrasTip);
        Grid.SetColumn(value, 0);
        panel.Children.Add(value);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            // Reserve so value columns stay aligned even when a metric has no buttons.
            MinWidth = cell.CanShowLine || (col.Kind == MetricKind.PointOnCurve && cell.HasPoint) ? 0 : 24,
        };
        Grid.SetColumn(actions, 1);
        panel.Children.Add(actions);

        if (col.Kind == MetricKind.PointOnCurve && cell.HasPoint)
        {
            var btn = CellButton("⊕");
            ToolTip.SetTip(btn, $"Flash at ({Fmt(cell.AtX!.Value)}, {Fmt(cell.AtY!.Value)})");
            btn.Click += (_, _) => _vm?.FlashPoint(row, col);
            actions.Children.Add(btn);

            // Place a persisted event line at this point's X. The metric point itself isn't saved
            // in the .DPX, so this lets the user pin it as an event line that is.
            var pinBtn = CellButton("📍");
            ToolTip.SetTip(pinBtn, $"Place event line at X = {Fmt(cell.AtX!.Value)}");
            pinBtn.Click += (_, _) => _vm?.PlaceEventLine(row, col);
            actions.Children.Add(pinBtn);
        }

        // ╱ control for any line-drawable metric (slope / mean / min / max). Clicking cycles the
        // label mode: Off → Line → Line+Number → Line+Label+Number → Off. The ToggleButton's
        // :checked accent shows whenever the line is drawn (any mode but Off); the tooltip spells
        // out what the next click does. Mode survives a pan/zoom rebuild via the cell's LabelMode.
        if (cell.CanShowLine)
        {
            var lineBtn = new ToggleButton
            {
                Content = "╱",
                IsChecked = cell.ShowOnPlot,
            };
            lineBtn.Classes.Add("dpx-cellbtn");
            ApplyLineButtonTip(lineBtn, cell.LabelMode);
            // Cycle through the VM (which advances LabelMode + draws/relabels/clears), then reflect
            // the new state on this button without a full table rebuild.
            lineBtn.Click += (_, _) =>
            {
                _vm?.CycleLine(row, col);
                lineBtn.IsChecked = cell.ShowOnPlot;
                ApplyLineButtonTip(lineBtn, cell.LabelMode);
            };
            actions.Children.Add(lineBtn);
        }

        return panel;
    }

    /// <summary>Tooltip describing what the next ╱ click will do, given the current mode.</summary>
    private static void ApplyLineButtonTip(Control btn, StatLineLabelMode mode)
    {
        var tip = mode switch
        {
            StatLineLabelMode.Off => "Show line on plot",
            StatLineLabelMode.Line => "Add value label",
            StatLineLabelMode.LineNumber => "Add metric name to label",
            _ => "Hide line on plot",
        };
        ToolTip.SetTip(btn, tip);
    }

    private static Button CellButton(string glyph)
    {
        var btn = new Button { Content = glyph };
        btn.Classes.Add("dpx-cellbtn");
        return btn;
    }

    /// <summary>
    /// Rebuild the "Tolerance Bands" section: a fixed-column block (Curve, Limits, % In-Band,
    /// Crossings, Exceedance, Max Excursion, and a remove ✕) — one row per banded curve.
    /// </summary>
    private void RebuildBands()
    {
        if (_vm is null) return;
        var grid = this.FindControl<Grid>("BandGrid");
        if (grid is null) return;

        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        string[] headers = { "Curve", "Limits", "In-Band", "Crossings", "Exceedance", "Max Excursion", "" };
        foreach (var _ in headers)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < headers.Length; c++)
            AddHeaderCell(grid, headers[c], 0, c);

        for (int r = 0; r < _vm.BandRows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var b = _vm.BandRows[r];
            int gridRow = r + 1;

            // Zebra band behind the row (matches the results table family).
            var band = new Border
            {
                Background = (r % 2 == 1) ? Raised : Brushes.Transparent,
                BorderBrush = Divider,
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            band.Classes.Add("dpx-rowband");
            Grid.SetRow(band, gridRow);
            Grid.SetColumn(band, 0);
            Grid.SetColumnSpan(band, headers.Length);
            grid.Children.Add(band);

            var namePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(10, 5),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            namePanel.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(3),
                Background = SafeColor(b.ColorHex),
                BoxShadow = BoxShadows.Parse("inset 0 0 0 0.5 #33000000"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            var nameText = new TextBlock
            {
                Text = b.DisplayName,
                Foreground = TextMain,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(nameText, $"Scope: {b.ScopeLabel}");
            namePanel.Children.Add(nameText);
            Place(grid, namePanel, gridRow, 0);

            Place(grid, BandText(b.LimitsText), gridRow, 1);
            Place(grid, BandText(b.InBandText), gridRow, 2);
            Place(grid, BandText(b.CrossingsText), gridRow, 3);
            Place(grid, BandText(b.ExceedanceText), gridRow, 4);
            Place(grid, BandText(b.MaxExcursionText), gridRow, 5);

            var remove = new Button { Content = "✕" };
            remove.Classes.Add("dpx-ghostdanger");
            ToolTip.SetTip(remove, "Remove this tolerance band");
            var captured = b;
            remove.Click += (_, _) => _vm?.RemoveBand(captured);
            Place(grid, remove, gridRow, 6);
        }
    }

    private TextBlock BandText(string text) => new()
    {
        Text = text,
        FontFamily = MonoFamily,
        FontSize = 12,
        Foreground = TextMain,
        Margin = new Thickness(10, 5),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static void Place(Grid grid, Control el, int row, int col)
    {
        Grid.SetRow(el, row);
        Grid.SetColumn(el, col);
        grid.Children.Add(el);
    }

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static SolidColorBrush SafeColor(string hex)
    {
        try { return new SolidColorBrush(Color.Parse(hex)); }
        catch { return new SolidColorBrush(Colors.Gray); }
    }
}
