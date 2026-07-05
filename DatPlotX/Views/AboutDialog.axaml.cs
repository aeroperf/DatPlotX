using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DatPlotX.Helpers;

namespace DatPlotX.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        this.EnableEscapeToClose();
        var raw = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.0.0";
        var plus = raw.IndexOf('+');
        var version = plus >= 0 ? raw[..plus] : raw;
        this.FindControl<TextBlock>("VersionText")!.Text = $"Version {version}";
    }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Licenses_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new LicensesDialog();
        await dialog.ShowDialog(this);
    }
}
