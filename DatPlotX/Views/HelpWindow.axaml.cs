using Avalonia.Controls;
using DatPlotX.Helpers;
using DatPlotX.ViewModels;

namespace DatPlotX.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        DataContext = new HelpWindowViewModel();
        this.EnableEscapeToClose();
    }
}
