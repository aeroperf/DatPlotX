using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Models;
using System.Globalization;

namespace DatPlotX.ViewModels;

/// <summary>
/// Editable state for <see cref="Views.FormatPaneDialog"/>. Mirrors the writable surface of
/// <see cref="PlotPaneModel"/> as bindable properties and provides Load/Apply round-trips
/// against the model. Keeps the dialog code-behind free of business logic so the dialog
/// becomes unit-testable without a UI thread.
/// </summary>
public sealed partial class FormatPaneDialogViewModel : ObservableObject
{
    public string[] TitleFontStyleOptions { get; } = ["Normal", "Bold", "Italic"];
    public string[] GridLineWidthOptions { get; } = ["0.5", "1.0", "1.5", "2.0"];
    public string[] GridLineStyleOptions { get; } = ["Solid", "Dashed", "Dotted"];
    public string[] TickNumberFormatOptions { get; } = ["Standard", "Scientific"];
    public string[] DecimalsOptions { get; } = ["0", "1", "2", "3", "4", "5"];

    /// <summary>
    /// Display strings for each <see cref="LegendPosition"/> in dropdown order.
    /// Order intentional: Hidden first, then inside corners (existing behavior),
    /// then outside edges (new). String values map 1:1 to the enum names via
    /// <see cref="Enum.Parse{TEnum}(string)"/> in <see cref="ApplyTo"/>.
    /// </summary>
    public string[] LegendPositionOptions { get; } =
    [
        "Hidden",
        "Inside upper-left",
        "Inside upper-right",
        "Inside lower-left",
        "Inside lower-right",
    ];

    [ObservableProperty] private string _titleText = string.Empty;
    [ObservableProperty] private string _titleFontSizeText = "20";
    [ObservableProperty] private string _titleFontStyle = "Bold";

    [ObservableProperty] private string _xAxisLabel = string.Empty;
    [ObservableProperty] private string _y1AxisLabel = string.Empty;
    [ObservableProperty] private string _y2AxisLabel = string.Empty;
    [ObservableProperty] private bool _showY2Axis;

    [ObservableProperty] private string _axisLabelFontSizeText = "14";
    [ObservableProperty] private bool _axisLabelBold;

    [ObservableProperty] private string _tickLabelFontSizeText = "14";

    [ObservableProperty] private bool _xAutoScale = true;
    [ObservableProperty] private string? _xMinText;
    [ObservableProperty] private string? _xMaxText;

    [ObservableProperty] private bool _y1AutoScale = true;
    [ObservableProperty] private string? _y1MinText;
    [ObservableProperty] private string? _y1MaxText;

    [ObservableProperty] private bool _y2AutoScale = true;
    [ObservableProperty] private string? _y2MinText;
    [ObservableProperty] private string? _y2MaxText;

    /// <summary>Non-null when the current axis-range inputs are invalid; shown in the footer.</summary>
    [ObservableProperty] private string? _validationMessage;

    [ObservableProperty] private bool _showMajorGrid = true;
    [ObservableProperty] private bool _showMinorGrid;
    [ObservableProperty] private string _gridColor = "#E0E0E0";
    [ObservableProperty] private string _gridLineWidth = "1.0";
    [ObservableProperty] private string _gridLineStyle = "Solid";

    [ObservableProperty] private string _tickNumberFormat = "Standard";
    [ObservableProperty] private string _xAxisDecimalPlaces = "0";
    [ObservableProperty] private string _y1AxisDecimalPlaces = "0";
    [ObservableProperty] private string _y2AxisDecimalPlaces = "0";

    [ObservableProperty] private string _xAxisFormatPreview = string.Empty;
    [ObservableProperty] private string _y1AxisFormatPreview = string.Empty;
    [ObservableProperty] private string _y2AxisFormatPreview = string.Empty;

    [ObservableProperty] private string _backgroundColor = "#FFFFFF";
    [ObservableProperty] private string _dataBackgroundColor = "#FFFFFF";

    [ObservableProperty] private string _legendFontSizeText = "14";
    [ObservableProperty] private string _legendPosition = "Inside upper-right";

    public void LoadFrom(PlotPaneModel model)
    {
        TitleText = model.TitleText;
        TitleFontSizeText = model.TitleFontSize.ToString(CultureInfo.InvariantCulture);
        TitleFontStyle = model.TitleFontStyle;

        XAxisLabel = model.XAxisLabel;
        Y1AxisLabel = model.YAxisLabel;
        Y2AxisLabel = model.Y2AxisLabel;
        ShowY2Axis = model.ShowY2Axis;

        AxisLabelFontSizeText = model.AxisLabelFontSize.ToString(CultureInfo.InvariantCulture);
        AxisLabelBold = model.AxisLabelBold;
        TickLabelFontSizeText = model.TickLabelFontSize.ToString(CultureInfo.InvariantCulture);

        XAutoScale = !model.XAxisMin.HasValue && !model.XAxisMax.HasValue;
        Y1AutoScale = !model.YAxisMin.HasValue && !model.YAxisMax.HasValue;
        Y2AutoScale = !model.Y2AxisMin.HasValue && !model.Y2AxisMax.HasValue;
        XMinText = model.XAxisMin?.ToString(CultureInfo.InvariantCulture);
        XMaxText = model.XAxisMax?.ToString(CultureInfo.InvariantCulture);
        Y1MinText = model.YAxisMin?.ToString(CultureInfo.InvariantCulture);
        Y1MaxText = model.YAxisMax?.ToString(CultureInfo.InvariantCulture);
        Y2MinText = model.Y2AxisMin?.ToString(CultureInfo.InvariantCulture);
        Y2MaxText = model.Y2AxisMax?.ToString(CultureInfo.InvariantCulture);

        ShowMajorGrid = model.ShowMajorGrid;
        ShowMinorGrid = model.ShowMinorGrid;
        GridColor = model.GridColor;
        GridLineWidth = model.GridLineWidth.ToString("F1", CultureInfo.InvariantCulture);
        GridLineStyle = model.GridLineStyle;

        TickNumberFormat = model.TickNumberFormat;
        XAxisDecimalPlaces = model.XAxisDecimalPlaces.ToString(CultureInfo.InvariantCulture);
        Y1AxisDecimalPlaces = model.Y1AxisDecimalPlaces.ToString(CultureInfo.InvariantCulture);
        Y2AxisDecimalPlaces = model.Y2AxisDecimalPlaces.ToString(CultureInfo.InvariantCulture);

        BackgroundColor = model.BackgroundColor;
        DataBackgroundColor = model.DataBackgroundColor;

        LegendFontSizeText = model.LegendFontSize.ToString(CultureInfo.InvariantCulture);
        LegendPosition = LegendPositionToDisplay(model.LegendPosition);

        UpdateFormatPreview();
    }

    /// <summary>
    /// Returns a user-facing error if any manually-ranged axis has min ≥ max, else null. Without
    /// this an inverted range flowed straight into ScottPlot's Range.Set(min, max) and produced a
    /// silently flipped axis. Only bounds where BOTH ends are provided are checked (a one-sided
    /// bound fills the other end from the live range at apply time).
    /// </summary>
    public string? Validate()
    {
        if (RangeInverted(XAutoScale, XMinText, XMaxText)) return "X axis minimum must be less than maximum.";
        if (RangeInverted(Y1AutoScale, Y1MinText, Y1MaxText)) return "Y1 axis minimum must be less than maximum.";
        if (RangeInverted(Y2AutoScale, Y2MinText, Y2MaxText)) return "Y2 axis minimum must be less than maximum.";
        return null;
    }

    private static bool RangeInverted(bool autoScale, string? minText, string? maxText)
    {
        if (autoScale) return false;
        if (!TryParseDouble(minText, out var min) || !TryParseDouble(maxText, out var max)) return false;
        return min >= max;
    }

    public void ApplyTo(PlotPaneModel model)
    {
        model.TitleText = TitleText;
        if (TryParseDouble(TitleFontSizeText, out var titleFs)) model.TitleFontSize = titleFs;
        model.TitleFontStyle = TitleFontStyle;

        model.XAxisLabel = XAxisLabel;
        model.YAxisLabel = Y1AxisLabel;
        model.Y2AxisLabel = Y2AxisLabel;
        model.ShowY2Axis = ShowY2Axis;

        if (TryParseDouble(AxisLabelFontSizeText, out var labelFs)) model.AxisLabelFontSize = labelFs;
        model.AxisLabelBold = AxisLabelBold;
        if (TryParseDouble(TickLabelFontSizeText, out var tickFs)) model.TickLabelFontSize = tickFs;

        ApplyRange(XAutoScale, XMinText, XMaxText, v => model.XAxisMin = v, v => model.XAxisMax = v);
        ApplyRange(Y1AutoScale, Y1MinText, Y1MaxText, v => model.YAxisMin = v, v => model.YAxisMax = v);
        ApplyRange(Y2AutoScale, Y2MinText, Y2MaxText, v => model.Y2AxisMin = v, v => model.Y2AxisMax = v);

        model.ShowMajorGrid = ShowMajorGrid;
        model.ShowMinorGrid = ShowMinorGrid;
        model.GridColor = GridColor;
        if (TryParseDouble(GridLineWidth, out var gw)) model.GridLineWidth = gw;
        model.GridLineStyle = GridLineStyle;

        model.TickNumberFormat = TickNumberFormat;
        if (TryParseInt(XAxisDecimalPlaces, out var xd)) model.XAxisDecimalPlaces = xd;
        if (TryParseInt(Y1AxisDecimalPlaces, out var y1d)) model.Y1AxisDecimalPlaces = y1d;
        if (TryParseInt(Y2AxisDecimalPlaces, out var y2d)) model.Y2AxisDecimalPlaces = y2d;

        model.BackgroundColor = BackgroundColor;
        model.DataBackgroundColor = DataBackgroundColor;

        if (TryParseDouble(LegendFontSizeText, out var legFs)) model.LegendFontSize = legFs;
        model.LegendPosition = DisplayToLegendPosition(LegendPosition);
        // Keep the legacy ShowLegend bool in sync so existing curve-manager code that
        // skips legend creation when ShowLegend is false still behaves predictably.
        model.ShowLegend = model.LegendPosition != Models.LegendPosition.Hidden;
    }

    private static string LegendPositionToDisplay(Models.LegendPosition position) => position switch
    {
        Models.LegendPosition.Hidden => "Hidden",
        Models.LegendPosition.InsideUpperLeft => "Inside upper-left",
        Models.LegendPosition.InsideUpperRight => "Inside upper-right",
        Models.LegendPosition.InsideLowerLeft => "Inside lower-left",
        Models.LegendPosition.InsideLowerRight => "Inside lower-right",
        _ => "Inside upper-right",
    };

    private static Models.LegendPosition DisplayToLegendPosition(string display) => display switch
    {
        "Hidden" => Models.LegendPosition.Hidden,
        "Inside upper-left" => Models.LegendPosition.InsideUpperLeft,
        "Inside upper-right" => Models.LegendPosition.InsideUpperRight,
        "Inside lower-left" => Models.LegendPosition.InsideLowerLeft,
        "Inside lower-right" => Models.LegendPosition.InsideLowerRight,
        _ => Models.LegendPosition.InsideUpperRight,
    };

    partial void OnTickNumberFormatChanged(string value) => UpdateFormatPreview();
    partial void OnXAxisDecimalPlacesChanged(string value) => UpdateFormatPreview();
    partial void OnY1AxisDecimalPlacesChanged(string value) => UpdateFormatPreview();
    partial void OnY2AxisDecimalPlacesChanged(string value) => UpdateFormatPreview();

    private void UpdateFormatPreview()
    {
        const double sample = 12345.6789;
        XAxisFormatPreview = FormatSample(sample, TickNumberFormat, XAxisDecimalPlaces);
        Y1AxisFormatPreview = FormatSample(sample, TickNumberFormat, Y1AxisDecimalPlaces);
        Y2AxisFormatPreview = FormatSample(sample, TickNumberFormat, Y2AxisDecimalPlaces);
    }

    private static string FormatSample(double value, string format, string decimalsText)
    {
        if (!int.TryParse(decimalsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decimals))
            decimals = 0;
        return format switch
        {
            "Scientific" => value.ToString($"E{decimals}", CultureInfo.InvariantCulture),
            _ => value.ToString($"F{decimals}", CultureInfo.InvariantCulture),
        };
    }

    private static bool TryParseDouble(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool TryParseInt(string? text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static void ApplyRange(bool autoScale, string? minText, string? maxText,
        System.Action<double?> setMin, System.Action<double?> setMax)
    {
        if (autoScale) { setMin(null); setMax(null); return; }
        setMin(TryParseDouble(minText, out var min) ? min : null);
        setMax(TryParseDouble(maxText, out var max) ? max : null);
    }
}
