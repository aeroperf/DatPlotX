using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Content;

namespace DatPlotX.ViewModels;

public sealed partial class WhatsNewWindowViewModel : ObservableObject
{
    public List<ChangelogEntry> ChangelogEntries { get; } =
        AppChangelogContent.GetEntries().ToList();
}
