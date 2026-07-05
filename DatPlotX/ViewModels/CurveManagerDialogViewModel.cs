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

    partial void OnIsVisibleChanged(bool value)
    {
        Configuration.IsVisible = value;
    }

    partial void OnColorChanged(string value)
    {
        Configuration.Color = value;
    }

    partial void OnLineWidthChanged(double value)
    {
        Configuration.LineWidth = value;
    }

    partial void OnLineStyleChanged(LineStyle value)
    {
        Configuration.LineStyle = value;
    }

    [RelayCommand]
    private void MarkForRemoval()
    {
        IsMarkedForRemoval = !IsMarkedForRemoval;
    }
}
