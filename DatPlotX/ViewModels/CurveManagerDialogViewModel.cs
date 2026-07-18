using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatPlotX.Models;
using System.Collections.ObjectModel;

namespace DatPlotX.ViewModels;

/// <summary>
/// ViewModel for the Curve Manager dialog
/// </summary>
public partial class CurveManagerDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<CurveItemViewModel> _curves;

    [ObservableProperty]
    private CurveItemViewModel? _selectedCurve;

    public CurveManagerDialogViewModel(ObservableCollection<CurveConfigurationModel> activeCurves)
    {
        _curves = new ObservableCollection<CurveItemViewModel>();

        // Create view models for each curve
        foreach (var curve in activeCurves)
        {
            _curves.Add(new CurveItemViewModel(curve));
        }
    }

    /// <summary>
    /// Commit the edits from every row back into the live curve configuration models.
    /// Must be called by the dialog on OK/Apply only — until then the edits live purely on
    /// the row view models, so a Cancel leaves the live models (and the plot) untouched.
    /// </summary>
    public void ApplyChanges()
    {
        foreach (var c in Curves)
            c.ApplyChanges();
    }

    /// <summary>
    /// Get the modified curve configurations
    /// </summary>
    public List<CurveConfigurationModel> GetModifiedCurves()
    {
        return Curves.Select(c => c.Configuration).ToList();
    }

    /// <summary>
    /// Get curves marked for removal
    /// </summary>
    public List<CurveConfigurationModel> GetCurvesToRemove()
    {
        return Curves.Where(c => c.IsMarkedForRemoval).Select(c => c.Configuration).ToList();
    }
}

/// <summary>
/// ViewModel for a single curve item in the manager
/// </summary>
public partial class CurveItemViewModel : ObservableObject
{
    public CurveConfigurationModel Configuration { get; }

    [ObservableProperty]
    private bool _isMarkedForRemoval;

    public string CurveName => Configuration.CurveName;
    public string YColumnName => Configuration.YColumnName;
    public int PaneIndex => Configuration.PaneIndex;
    public string PaneDisplay => $"Pane {PaneIndex + 1}";
    public string YAxisDisplay => Configuration.YAxis == YAxisType.Y1 ? "Left (Y1)" : "Right (Y2)";

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _color;

    [ObservableProperty]
    private double _lineWidth;

    [ObservableProperty]
    private LineStyle _lineStyle;

    public CurveItemViewModel(CurveConfigurationModel config)
    {
        Configuration = config;
        _isVisible = config.IsVisible;
        _color = config.Color;
        _lineWidth = config.LineWidth;
        _lineStyle = config.LineStyle;
    }

    /// <summary>
    /// Copy this row's edited values into the live configuration model. Called only when the
    /// dialog is accepted (OK/Apply); the property setters no longer mutate the model directly,
    /// so a Cancel discards these edits instead of leaking them into the plot and the .DPX.
    /// </summary>
    public void ApplyChanges()
    {
        Configuration.IsVisible = IsVisible;
        Configuration.Color = Color;
        Configuration.LineWidth = LineWidth;
        Configuration.LineStyle = LineStyle;
    }

    [RelayCommand]
    private void MarkForRemoval()
    {
        IsMarkedForRemoval = !IsMarkedForRemoval;
    }
}
