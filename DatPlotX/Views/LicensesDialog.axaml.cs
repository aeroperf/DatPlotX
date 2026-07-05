using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using DatPlotX.Helpers;

namespace DatPlotX.Views;

public partial class LicensesDialog : Window
{
    public LicensesDialog()
    {
        InitializeComponent();
        this.EnableEscapeToClose();
        SetText("avares://DatPlotX/Assets/LICENSE.txt");
    }

    private void SetText(string assetUri)
    {
        var target = this.FindControl<TextBlock>("LicenseText")!;
        try
        {
            using var stream = AssetLoader.Open(new Uri(assetUri));
            using var reader = new StreamReader(stream);
            target.Text = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            target.Text = $"Unable to load license text.\n\n{ex.Message}";
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
