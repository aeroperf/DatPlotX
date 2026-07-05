using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Content;

namespace DatPlotX.ViewModels;

public sealed partial class HelpWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private HelpSection? _selectedSection;

    public ObservableCollection<HelpSection> Sections { get; }

    public HelpWindowViewModel()
    {
        var sections = AppHelpContent.GetSections();
        Sections = new ObservableCollection<HelpSection>(sections);

        if (sections.Count > 0)
            SelectedSection = sections[0];
    }
}
