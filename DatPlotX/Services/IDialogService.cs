using System.Collections.ObjectModel;
using DatPlotX.Models;
using DatPlotX.ViewModels;

namespace DatPlotX.Services;

/// <summary>
/// Custom dialog result enum replacing System.Windows.MessageBoxResult
/// </summary>
public enum DialogResult
{
    None,
    OK,
    Yes,
    No,
    Cancel
}

/// <summary>
/// Custom dialog buttons enum replacing System.Windows.MessageBoxButton
/// </summary>
public enum DialogButtons
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel
}

/// <summary>
/// Interface for dialog operations, enabling testability and dependency injection
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Show an information message
    /// </summary>
    Task ShowInformation(string message, string title = "Information");

    /// <summary>
    /// Show an error message
    /// </summary>
    Task ShowError(string message, string title = "Error");

    /// <summary>
    /// Show a warning message and return the user's choice
    /// </summary>
    Task<DialogResult> ShowWarning(string message, string title = "Warning", DialogButtons buttons = DialogButtons.OK);

    /// <summary>
    /// Show a confirmation dialog
    /// </summary>
    Task<DialogResult> ShowConfirmation(string message, string title = "Confirm");

    /// <summary>
    /// Show a Yes/No/Cancel dialog for unsaved changes
    /// </summary>
    Task<DialogResult> ShowUnsavedChangesDialog();

    /// <summary>
    /// Show the Settings dialog. Returns true if user clicked OK, false/null if cancelled.
    /// </summary>
    Task<bool?> ShowSettingsAsync(Models.ApplicationSettings settings);

    /// <summary>
    /// Show the modal "Choose Plot Style" picker for new projects and first-time CSV imports.
    /// Returns the selected <see cref="Models.PlotMode"/>, or <c>null</c> if the user cancelled.
    /// </summary>
    Task<Models.PlotMode?> ShowPlotModePickerAsync();

    /// <summary>
    /// Show the "Configure Grouped Inputs" wizard. Pre-fills from <paramref name="existing"/>
    /// when provided. Returns the new <see cref="GroupedPlotConfig"/> on Apply, or <c>null</c>
    /// on Cancel.
    /// </summary>
    Task<GroupedPlotConfig?> ShowGroupedInputsPickerAsync(PlotDataModel data, GroupedPlotConfig? existing);

    /// <summary>
    /// Show the Compact Pane formatting dialog seeded with the given settings. Returns the
    /// edited <see cref="Models.CompactPaneSettings"/> on OK, or <c>null</c> if the user cancelled.
    /// Keeps Avalonia view types out of <c>MainWindowViewModel</c> per the MVVM contract.
    /// </summary>
    Task<Models.CompactPaneSettings?> ShowFormatCompactPaneAsync(Models.CompactPaneSettings settings);

    /// <summary>
    /// Show the Compact Plot Surface "Add Curves" dialog. Returns the assembled curve list on OK,
    /// or <c>null</c> if the user cancelled.
    /// </summary>
    Task<IReadOnlyList<CompactCurveModel>?> ShowAddCompactCurvesAsync(
        PlotDataModel data,
        string xColumn,
        int existingCurveCount);

    /// <summary>
    /// Show the Compact Plot Surface "Manage Curve" dialog. The dialog mutates the selected curve
    /// in place; the result indicates whether the user requested deletion. Returns <c>null</c> if
    /// the user cancelled or there were no curves to manage.
    /// </summary>
    Task<ManageCompactCurveResult?> ShowManageCompactCurveAsync(ObservableCollection<CompactCurveModel> curves);

    /// <summary>
    /// Show the stacked-mode "Manage Curves" dialog. The dialog mutates its own VM in place;
    /// returns that VM on Apply, or <c>null</c> if the user cancelled.
    /// </summary>
    Task<CurveManagerDialogViewModel?> ShowCurveManagerAsync(ObservableCollection<CurveConfigurationModel> activeCurves);

    /// <summary>
    /// Show the "Manage Segments" dialog seeded with the current analysis segments + active id.
    /// Returns the dialog VM on Apply (host reconciles renames / deletes / active selection),
    /// or <c>null</c> if cancelled.
    /// </summary>
    Task<ManageSegmentsDialogViewModel?> ShowManageSegmentsAsync(
        IReadOnlyList<Models.Analysis.AnalysisSegment> segments, Guid activeId);

    /// <summary>
    /// Show the "Manage Metrics" dialog seeded with every registered metric and the currently
    /// enabled column set. Returns the dialog VM on Apply (host applies
    /// <see cref="ManageMetricsDialogViewModel.EnabledIds"/> to the analysis service), or
    /// <c>null</c> if cancelled.
    /// </summary>
    Task<ManageMetricsDialogViewModel?> ShowManageMetricsAsync(
        IReadOnlyList<Services.Analysis.IMetricDefinition> allMetrics, IReadOnlyList<string> enabledIds);

    /// <summary>
    /// Show the "Tolerance Band" dialog. <paramref name="curves"/> are the curves the band can
    /// attach to; <paramref name="previewResolver"/> resolves a candidate band's concrete limits
    /// for the live preview (host computes the derived center against real data). When
    /// <paramref name="existing"/> is non-null the dialog opens pre-populated for editing.
    /// Returns the configured band on Apply, or <c>null</c> on cancel.
    /// </summary>
    Task<Models.Analysis.ToleranceBand?> ShowToleranceBandAsync(
        IReadOnlyList<Views.ToleranceBandCurveChoice> curves,
        Func<Models.Analysis.ToleranceBand, (double Center, double Lower, double Upper)?>? previewResolver,
        Models.Analysis.ToleranceBand? existing = null);

    /// <summary>
    /// Show the stacked-mode "Add Curves" dialog. The dialog plots each curve via the supplied
    /// callback as the user clicks "Plot Curve"; the task completes when the user closes the
    /// dialog. There is no return value because all plotting happens through the callback.
    /// </summary>
    Task ShowAddCurvesAsync(
        System.Data.DataTable sourceData,
        string xColumn,
        int targetPaneIndex,
        Action<AddCurveRequest> onCurvePlotted);

    /// <summary>
    /// Show the Import Options dialog for the given file. Returns the user's chosen options
    /// on OK, or <c>null</c> if the dialog was cancelled. Keeps Avalonia view types out of
    /// <see cref="IFileOperationsService"/> per the MVVM contract.
    /// </summary>
    Task<Models.ImportOptionsModel?> ShowImportOptionsAsync(string filePath);

    /// <summary>
    /// Show the "Add Text Annotation" / "Edit Text Annotation" dialog. The dialog mutates an
    /// internal clone; on OK the edited model is returned. Returns <c>null</c> on cancel.
    /// </summary>
    Task<Models.TextAnnotationModel?> ShowTextAnnotationDialogAsync(Models.TextAnnotationModel seed);

    /// <summary>
    /// Show the "Add Arrow Annotation" / "Edit Arrow Annotation" dialog. The dialog mutates an
    /// internal clone; on OK the edited model is returned. Returns <c>null</c> on cancel.
    /// </summary>
    Task<Models.ArrowAnnotationModel?> ShowArrowAnnotationDialogAsync(Models.ArrowAnnotationModel seed);

    /// <summary>
    /// Show the modeless "About" dialog. Awaits the dialog close.
    /// </summary>
    Task ShowAboutAsync();

    /// <summary>
    /// Show the modeless "What's New" window. Awaits the window close.
    /// </summary>
    Task ShowWhatsNewAsync();

    /// <summary>
    /// Show the modeless User Guide / Help window. Awaits the window close.
    /// </summary>
    Task ShowUserGuideAsync();

    /// <summary>
    /// Show the Format Curve dialog scoped to the given pane. Returns the selected curve plus
    /// a delete-requested flag on OK, or <c>null</c> if cancelled.
    /// </summary>
    Task<FormatCurveResult?> ShowFormatCurveAsync(
        ObservableCollection<CurveConfigurationModel> activeCurves,
        int paneIndex);

    /// <summary>
    /// Show the Format Pane dialog. The dialog mutates the supplied <see cref="Models.PlotPaneModel"/>
    /// in place. Returns <c>true</c> on OK, <c>false</c> / <c>null</c> on cancel.
    /// </summary>
    Task<bool?> ShowFormatPaneAsync(Models.PlotPaneModel paneModel);

    /// <summary>
    /// Show the "Add Event Line" dialog seeded at the given X position with a suggested label.
    /// Returns the label/color the user chose on OK, or <c>null</c> on cancel / empty label.
    /// </summary>
    Task<AddEventLineResult?> ShowAddEventLineAsync(double xPosition, string suggestedLabel);
}

/// <summary>
/// Result of <see cref="IDialogService.ShowFormatCurveAsync"/>. The dialog mutates
/// <see cref="Curve"/> in place; callers should call <c>RemoveCurve</c> when
/// <see cref="DeleteRequested"/> is true, otherwise <c>UpdateCurveFormat</c>.
/// </summary>
public sealed record FormatCurveResult(CurveConfigurationModel Curve, bool DeleteRequested);

/// <summary>
/// Result of <see cref="IDialogService.ShowAddEventLineAsync"/>.
/// </summary>
public sealed record AddEventLineResult(string LabelText, string ColorHex);

/// <summary>
/// Result of <see cref="IDialogService.ShowManageCompactCurveAsync"/>. The dialog mutates
/// <see cref="Curve"/> in place; callers should call <c>RemoveCurve</c> when
/// <see cref="DeleteRequested"/> is true, otherwise <c>UpdateCurve</c>.
/// </summary>
public sealed record ManageCompactCurveResult(CompactCurveModel Curve, bool DeleteRequested);
