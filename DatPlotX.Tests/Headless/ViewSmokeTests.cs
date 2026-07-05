using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;

namespace DatPlotX.Tests.Headless;

/// <summary>
/// XAML-load smoke test for every view in the application. Each <see cref="Window"/> /
/// <see cref="UserControl"/> under <c>DatPlotX.Views</c> is constructed through its parameterless
/// (designer) constructor and forced through a headless layout pass. This catches the whole class of
/// regressions that otherwise only surface by running the app: a renamed <c>StaticResource</c>, a
/// dropped style include, a broken compiled binding, or a missing <c>x:DataType</c> — any of which
/// throws when the XAML is realized rather than at compile time.
///
/// <para>
/// The test is data-driven off reflection, so new views are covered automatically with no edit here.
/// Every view in the codebase exposes a parameterless designer constructor by convention; if one is
/// ever added without one, <see cref="ViewTypes"/> simply skips it (and the count assertion below
/// guards against the list silently emptying).
/// </para>
/// </summary>
public class ViewSmokeTests
{
    /// <summary>All concrete view types that expose a parameterless constructor, newest reflection.</summary>
    public static IEnumerable<object[]> ViewTypes()
        => typeof(MainWindow).Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith("DatPlotX.Views", StringComparison.Ordinal) == true)
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => typeof(Control).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => new object[] { t });

    [AvaloniaTheory]
    [MemberData(nameof(ViewTypes))]
    public void View_LoadsAndLaysOut_WithoutThrowing(Type viewType)
    {
        var control = (Control)Activator.CreateInstance(viewType)!;

        // Host UserControls inside a window; windows are top-levels already. Showing + laying out
        // forces InitializeComponent's realized tree through measure/arrange — where XAML resolution
        // errors actually throw.
        Window window;
        if (control is Window w)
        {
            window = w;
        }
        else
        {
            window = new Window { Content = control, Width = 800, Height = 600 };
        }

        window.Show();
        window.UpdateLayout();

        window.IsVisible.Should().BeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void ViewTypes_DiscoversTheExpectedViewCount()
    {
        // Guard against the reflection query silently returning nothing (e.g. a namespace rename),
        // which would make the theory above pass vacuously. Lower bound, not exact — new views are
        // welcome without touching this test.
        ViewTypes().Should().HaveCountGreaterThanOrEqualTo(30);
    }
}
