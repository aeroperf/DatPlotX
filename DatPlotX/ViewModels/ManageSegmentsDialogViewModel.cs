using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatPlotX.Models.Analysis;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DatPlotX.ViewModels;

/// <summary>
/// ViewModel for the Manage Segments dialog. Works on an editable snapshot of the analysis
/// segments; the host reconciles the result (renames, deletions, active selection) against
/// <c>IAnalysisService</c> on Apply, so Cancel leaves the live state untouched.
/// </summary>
public partial class ManageSegmentsDialogViewModel : ObservableObject
{
    public ManageSegmentsDialogViewModel(IReadOnlyList<AnalysisSegment> segments, Guid activeId)
    {
        foreach (var s in segments)
            Rows.Add(new SegmentRowViewModel(s));

        SelectedRow = Rows.FirstOrDefault(r => r.Id == activeId) ?? Rows.FirstOrDefault();
    }

    /// <summary>Design-time / fallback ctor.</summary>
    public ManageSegmentsDialogViewModel()
        : this(new[] { AnalysisSegment.VisibleWindow(0, 0) }, Guid.Empty)
    {
    }

    public ObservableCollection<SegmentRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private SegmentRowViewModel? _selectedRow;

    /// <summary>The segment chosen to be active after Apply (the selected row), or null for none.</summary>
    public Guid? ActiveId => SelectedRow is { IsMarkedForRemoval: false } r ? r.Id : null;

    /// <summary>Rows the user marked for deletion (never includes the visible-window segment).</summary>
    public IReadOnlyList<SegmentRowViewModel> ToRemove =>
        Rows.Where(r => r.IsMarkedForRemoval && r.CanDelete).ToList();

    /// <summary>Rows whose name changed from the original. Blank/whitespace names are ignored so a
    /// cleared name can't rename a segment to an empty string (it would show blank in the panel).</summary>
    public IReadOnlyList<SegmentRowViewModel> Renamed =>
        Rows.Where(r => r is { CanDelete: true, IsMarkedForRemoval: false }
                        && r.Name != r.OriginalName
                        && !string.IsNullOrWhiteSpace(r.Name)).ToList();
}

/// <summary>One editable segment row in the Manage Segments dialog.</summary>
public partial class SegmentRowViewModel : ObservableObject
{
    public SegmentRowViewModel(AnalysisSegment segment)
    {
        Id = segment.Id;
        OriginalName = segment.Name;
        _name = segment.Name;
        Source = segment.Source;
        XMin = segment.XMin;
        XMax = segment.XMax;
    }

    public Guid Id { get; }
    public string OriginalName { get; }
    public AnalysisSegmentSource Source { get; }
    public double XMin { get; }
    public double XMax { get; }

    /// <summary>The visible-window segment is implicit and managed internally — never editable/deletable.</summary>
    public bool CanDelete => Source != AnalysisSegmentSource.VisibleWindow;
    public bool CanRename => CanDelete;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isMarkedForRemoval;

    /// <summary>Toggle this row's removal mark (no-op for the visible-window segment).</summary>
    [RelayCommand]
    private void MarkForRemoval()
    {
        if (CanDelete) IsMarkedForRemoval = !IsMarkedForRemoval;
    }

    public string SourceDisplay => Source switch
    {
        AnalysisSegmentSource.VisibleWindow => "Visible window",
        AnalysisSegmentSource.FullData => "Full data",
        AnalysisSegmentSource.EventLinePair => "Event lines",
        _ => "Manual",
    };

    public string RangeDisplay => Source == AnalysisSegmentSource.VisibleWindow
        ? "(tracks view)"
        : string.Create(CultureInfo.InvariantCulture, $"{XMin:0.###} … {XMax:0.###}");
}
