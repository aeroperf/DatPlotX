using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Globalization;

namespace DatPlotX.Views;

/// <summary>
/// Modal dialog for adding a global event line to all stacked panes. Captures the user-facing
/// label and color; the X-coordinate is fixed by the caller (right-click position).
/// Returns true on accept; <see cref="LabelText"/> + <see cref="ColorHex"/> hold the result.
/// </summary>
public partial class AddEventLineDialog : Window
{
    public string LabelText { get; private set; } = string.Empty;
    public string ColorHex { get; private set; } = "#FFB900";

    public AddEventLineDialog(double xPosition, string suggestedLabel, string color = "#FFB900")
    {
        InitializeComponent();
        XPositionText.Text = $"x = {xPosition.ToString("F3", CultureInfo.InvariantCulture)}";
        LabelTextBox.Text = suggestedLabel;
        ColorHex = color;
        ColorPickerPill.Color = color;

        OkButton.Click += OK_Click;
        CancelButton.Click += (_, _) => Close(false);
        ColorPickerPill.PickRequested += (_, _) => SafeInvokeAsync(async () =>
        {
            var dlg = new ColorPickerDialog(ColorHex);
            if (await dlg.ShowDialog<bool?>(this) == true)
            {
                ColorHex = dlg.SelectedColor;
                ColorPickerPill.Color = ColorHex;
            }
        });
        LabelTextBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) OK_Click(this, new RoutedEventArgs());
            else if (e.Key == Key.Escape) Close(false);
        };

        Opened += (_, _) => { LabelTextBox.SelectAll(); LabelTextBox.Focus(); };
    }

    public AddEventLineDialog() : this(0, "E1") { }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        var text = LabelTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return;
        LabelText = text;
        Close(true);
    }

    private async void SafeInvokeAsync(Func<System.Threading.Tasks.Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AddEventLineDialog] {ex}"); }
    }
}
