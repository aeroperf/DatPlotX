using Avalonia.Controls;
using DatPlotX.Helpers;
using DatPlotX.ViewModels;

namespace DatPlotX.Views;

public partial class WhatsNewWindow : Window
{
    public WhatsNewWindow()
    {
        InitializeComponent();
        DataContext = new WhatsNewWindowViewModel();
        this.EnableEscapeToClose();
    }
}
