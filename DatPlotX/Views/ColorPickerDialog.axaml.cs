using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DatPlotX.Helpers;

namespace DatPlotX.Views;

/// <summary>
/// Custom color picker dialog with a palette grid of named colors plus a hex input.
/// Returns Close(true) with <see cref="SelectedColor"/> set on OK.
/// </summary>
public partial class ColorPickerDialog : Window
{
    public string SelectedColor { get; private set; }

    public ColorPickerDialog(string initialColor = "#0078D4")
    {
        InitializeComponent();

        SelectedColor = initialColor;
        UpdateSelectedColorDisplay(initialColor);

        LoadColorPalette();
        this.EnableEscapeToClose(false);
    }

    public ColorPickerDialog() : this("#0078D4") { }

    private void LoadColorPalette()
    {
        var colors = new[]
        {
            new ColorItem("#FF0000", "Red"),
            new ColorItem("#DC143C", "Crimson"),
            new ColorItem("#FF6347", "Tomato"),
            new ColorItem("#FF4500", "OrangeRed"),
            new ColorItem("#FF8C00", "DarkOrange"),

            new ColorItem("#FFA500", "Orange"),
            new ColorItem("#FFD700", "Gold"),
            new ColorItem("#FFFF00", "Yellow"),
            new ColorItem("#FFFFE0", "LightYellow"),
            new ColorItem("#F0E68C", "Khaki"),

            new ColorItem("#00FF00", "Lime"),
            new ColorItem("#32CD32", "LimeGreen"),
            new ColorItem("#00FF7F", "SpringGreen"),
            new ColorItem("#00FA9A", "MediumSpringGreen"),
            new ColorItem("#90EE90", "LightGreen"),
            new ColorItem("#228B22", "ForestGreen"),
            new ColorItem("#008000", "Green"),
            new ColorItem("#006400", "DarkGreen"),

            new ColorItem("#00FFFF", "Cyan"),
            new ColorItem("#00CED1", "DarkTurquoise"),
            new ColorItem("#4682B4", "SteelBlue"),
            new ColorItem("#1E90FF", "DodgerBlue"),
            new ColorItem("#0000FF", "Blue"),
            new ColorItem("#0078D4", "Azure Blue"),
            new ColorItem("#0000CD", "MediumBlue"),
            new ColorItem("#00008B", "DarkBlue"),
            new ColorItem("#000080", "Navy"),

            new ColorItem("#800080", "Purple"),
            new ColorItem("#9370DB", "MediumPurple"),
            new ColorItem("#8A2BE2", "BlueViolet"),
            new ColorItem("#9400D3", "DarkViolet"),
            new ColorItem("#FF00FF", "Magenta"),
            new ColorItem("#DA70D6", "Orchid"),

            new ColorItem("#FFC0CB", "Pink"),
            new ColorItem("#FF69B4", "HotPink"),
            new ColorItem("#FF1493", "DeepPink"),

            new ColorItem("#A52A2A", "Brown"),
            new ColorItem("#8B4513", "SaddleBrown"),
            new ColorItem("#D2691E", "Chocolate"),
            new ColorItem("#CD853F", "Peru"),
            new ColorItem("#F4A460", "SandyBrown"),
            new ColorItem("#D2B48C", "Tan"),

            new ColorItem("#000000", "Black"),
            new ColorItem("#2F4F4F", "DarkSlateGray"),
            new ColorItem("#696969", "DimGray"),
            new ColorItem("#808080", "Gray"),
            new ColorItem("#A9A9A9", "DarkGray"),
            new ColorItem("#C0C0C0", "Silver"),
            new ColorItem("#D3D3D3", "LightGray"),
            new ColorItem("#DCDCDC", "Gainsboro"),
            new ColorItem("#F5F5F5", "WhiteSmoke"),
            new ColorItem("#FFFFFF", "White"),
        };

        ColorPalette.ItemsSource = colors;

        // Apply selection ring once buttons are realized.
        ColorPalette.AttachedToVisualTree += (_, _) => RefreshSwatchSelection();
    }

    private void ColorButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string colorHex)
        {
            UpdateSelectedColorDisplay(colorHex);
            RefreshSwatchSelection();
        }
    }

    private void UpdateSelectedColorDisplay(string colorHex)
    {
        SelectedColor = colorHex;

        if (Color.TryParse(colorHex, out var color))
            SelectedColorPreview.Background = new SolidColorBrush(color);

        SelectedColorText.Text = colorHex.ToUpperInvariant();
        HexInputBox.Text = colorHex.ToUpperInvariant();
    }

    private void RefreshSwatchSelection()
    {
        foreach (var btn in ColorPalette.GetVisualDescendants().OfType<Button>())
        {
            if (btn.Classes.Contains("dpx-swatch"))
            {
                bool selected = string.Equals(btn.Tag as string, SelectedColor, StringComparison.OrdinalIgnoreCase);
                if (selected && !btn.Classes.Contains("selected"))
                    btn.Classes.Add("selected");
                else if (!selected && btn.Classes.Contains("selected"))
                    btn.Classes.Remove("selected");
            }
        }
    }

    private void HexInputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ApplyHexInput();
    }

    private void ApplyHex_Click(object? sender, RoutedEventArgs e) => ApplyHexInput();

    private void ApplyHexInput()
    {
        var hex = HexInputBox.Text?.Trim() ?? "";
        if (!hex.StartsWith('#'))
            hex = "#" + hex;

        if (Color.TryParse(hex, out _))
        {
            UpdateSelectedColorDisplay(hex);
            RefreshSwatchSelection();
        }
    }

    private void OK_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    public class ColorItem
    {
        public string ColorHex { get; }
        public string ColorName { get; }
        public ISolidColorBrush ColorBrush { get; }

        public ColorItem(string hex, string name)
        {
            ColorHex = hex;
            ColorName = name;
            ColorBrush = Color.TryParse(hex, out var color)
                ? new SolidColorBrush(color)
                : new SolidColorBrush(Colors.Transparent);
        }
    }
}
