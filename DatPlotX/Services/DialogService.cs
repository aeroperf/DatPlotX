using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Avalonia implementation of IDialogService using custom message box dialogs
/// </summary>
public class DialogService : IDialogService
{
    private readonly IFilePreviewService _previewService;
    private readonly IGroupedDataIndexer _groupedDataIndexer;
    private readonly ApplicationSettings _settings;

    public DialogService(IFilePreviewService previewService, IGroupedDataIndexer groupedDataIndexer, ApplicationSettings settings)
    {
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _groupedDataIndexer = groupedDataIndexer ?? throw new ArgumentNullException(nameof(groupedDataIndexer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task ShowInformation(string message, string title = "Information")
    {
        await ShowMessageBox(message, title, DialogButtons.OK);
    }

    public async Task ShowError(string message, string title = "Error")
    {
        await ShowMessageBox(message, title, DialogButtons.OK);
    }

    public async Task<DialogResult> ShowWarning(string message, string title = "Warning", DialogButtons buttons = DialogButtons.OK)
    {
        return await ShowMessageBox(message, title, buttons);
    }

    public async Task<DialogResult> ShowConfirmation(string message, string title = "Confirm")
    {
        return await ShowMessageBox(message, title, DialogButtons.YesNo);
    }

    public async Task<DialogResult> ShowUnsavedChangesDialog()
    {
        return await ShowMessageBox(
            "You have unsaved changes. Do you want to save before continuing?",
            "Unsaved Changes",
            DialogButtons.YesNoCancel);
    }

    public async Task<bool?> ShowSettingsAsync(Models.ApplicationSettings settings)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var vm = new ViewModels.SettingsDialogViewModel(settings);
        var dialog = new Views.SettingsDialog(vm);

        // Both branches must await a modal ShowDialog. The old else-branch used the non-blocking
        // Show(), so DialogAccepted was read before the user could interact — every change was
        // silently discarded when there was no owner window. Self-own the dialog as a fallback,
        // matching the other dialogs here.
        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            await dialog.ShowDialog(dialog);

        if (!dialog.DialogAccepted) return false;
        vm.ApplyTo(settings);
        return true;
    }

    public async Task<Models.PlotMode?> ShowPlotModePickerAsync()
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.PlotModePickerDialog();
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? dialog.SelectedMode : (Models.PlotMode?)null;
    }

    public async Task<GroupedPlotConfig?> ShowGroupedInputsPickerAsync(PlotDataModel data, GroupedPlotConfig? existing)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.GroupedInputsPickerDialog();
        dialog.Initialize(data, _groupedDataIndexer, _settings, existing);
        var result = owner is not null
            ? await dialog.ShowDialog<GroupedPlotConfig?>(owner)
            : await dialog.ShowDialog<GroupedPlotConfig?>(dialog);
        return result;
    }

    public async Task<Models.CompactPaneSettings?> ShowFormatCompactPaneAsync(Models.CompactPaneSettings settings)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.FormatCompactPaneDialog(settings);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? dialog.Result : null;
    }

    public async Task<IReadOnlyList<CompactCurveModel>?> ShowAddCompactCurvesAsync(
        PlotDataModel data, string xColumn, int existingCurveCount)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.AddCompactCurvesDialog(data, xColumn, existingCurveCount);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? dialog.Result : null;
    }

    public async Task<ManageCompactCurveResult?> ShowManageCompactCurveAsync(ObservableCollection<CompactCurveModel> curves)
    {
        if (curves.Count == 0) return null;
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.ManageCompactCurveDialog(curves);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        if (ok != true || dialog.SelectedCurve is not { } curve) return null;
        return new ManageCompactCurveResult(curve, dialog.DeleteRequested);
    }

    public async Task<CurveManagerDialogViewModel?> ShowCurveManagerAsync(ObservableCollection<CurveConfigurationModel> activeCurves)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.CurveManagerDialog(activeCurves);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? dialog.ViewModel : null;
    }

    public async Task<ManageSegmentsDialogViewModel?> ShowManageSegmentsAsync(
        IReadOnlyList<Models.Analysis.AnalysisSegment> segments, Guid activeId)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.ManageSegmentsDialog(segments, activeId);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? dialog.ViewModel : null;
    }

    public async Task<ManageMetricsDialogViewModel?> ShowManageMetricsAsync(
        IReadOnlyList<Services.Analysis.IMetricDefinition> allMetrics, IReadOnlyList<string> enabledIds)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.ManageMetricsDialog(allMetrics, enabledIds);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? dialog.ViewModel : null;
    }

    public async Task ShowAddCurvesAsync(
        System.Data.DataTable sourceData, string xColumn, int targetPaneIndex,
        Action<AddCurveRequest> onCurvePlotted)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.AddCurvesDialog(sourceData, xColumn, targetPaneIndex, onCurvePlotted);
        if (owner is not null)
            await dialog.ShowDialog<bool?>(owner);
        else
            await dialog.ShowDialog<bool?>(dialog);
    }

    public async Task<Models.TextAnnotationModel?> ShowTextAnnotationDialogAsync(Models.TextAnnotationModel seed)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.TextAnnotationDialog(seed);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? dialog.Result : null;
    }

    public async Task<Models.ArrowAnnotationModel?> ShowArrowAnnotationDialogAsync(Models.ArrowAnnotationModel seed)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.ArrowAnnotationDialog(seed);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? dialog.Result : null;
    }

    public async Task ShowAboutAsync()
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.AboutDialog();
        if (owner is not null) await dialog.ShowDialog(owner);
        else dialog.Show();
    }

    public async Task ShowWhatsNewAsync()
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.WhatsNewWindow();
        if (owner is not null) await dialog.ShowDialog(owner);
        else dialog.Show();
    }

    public async Task ShowUserGuideAsync()
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.HelpWindow();
        if (owner is not null) await dialog.ShowDialog(owner);
        else dialog.Show();
    }

    public async Task<FormatCurveResult?> ShowFormatCurveAsync(
        ObservableCollection<CurveConfigurationModel> activeCurves, int paneIndex)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.FormatCurveDialog(activeCurves, paneIndex);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        if (ok != true || dialog.SelectedCurve is not { } curve) return null;
        return new FormatCurveResult(curve, dialog.DeleteRequested);
    }

    public async Task<bool?> ShowFormatPaneAsync(Models.PlotPaneModel paneModel)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.FormatPaneDialog(paneModel);
        return owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
    }

    public async Task<AddEventLineResult?> ShowAddEventLineAsync(double xPosition, string suggestedLabel)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.AddEventLineDialog(xPosition, suggestedLabel);
        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        if (ok != true || string.IsNullOrWhiteSpace(dialog.LabelText)) return null;
        return new AddEventLineResult(dialog.LabelText, dialog.ColorHex);
    }

    public async Task<Models.Analysis.ToleranceBand?> ShowToleranceBandAsync(
        IReadOnlyList<Views.ToleranceBandCurveChoice> curves,
        Func<Models.Analysis.ToleranceBand, (double Center, double Lower, double Upper)?>? previewResolver,
        Models.Analysis.ToleranceBand? existing = null)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var dialog = new Views.ToleranceBandDialog(curves, previewResolver, existing);
        if (owner is not null) await dialog.ShowDialog(owner);
        else await dialog.ShowDialog(dialog);
        return dialog.Result;
    }

    public async Task<Models.ImportOptionsModel?> ShowImportOptionsAsync(string filePath)
    {
        var owner = AppWindowHelper.GetMainWindow();
        var vm = new ImportOptionsDialogViewModel(filePath, _previewService);
        var dialog = new Views.ImportOptionsDialog(vm);

        // Preload the preview here (with full exception routing) instead of relying on the
        // dialog's Opened async-void handler — keeps exceptions on this awaitable path.
        try
        {
            await vm.LoadPreviewAsync();
        }
        catch (Exception ex)
        {
            SafeErrorHandler.LogError(ex, "Loading import preview", filePath);
            await ShowError($"Error reading file preview: {ex.Message}", "Import Error");
            return null;
        }

        bool? ok = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowDialog<bool?>(dialog);
        return ok == true ? vm.GetImportOptions() : null;
    }

    private static async Task<DialogResult> ShowMessageBox(string message, string title, DialogButtons buttons)
    {
        var parent = AppWindowHelper.GetMainWindow();
        if (parent == null)
            return DialogResult.None;

        var result = DialogResult.None;

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.Full
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 20, 20, 10),
            VerticalAlignment = VerticalAlignment.Top
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 0, 20, 15),
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        void AddButton(string text, DialogResult dialogResult, bool isDefault = false)
        {
            var btn = new Button
            {
                Content = text,
                MinWidth = 80,
                IsDefault = isDefault
            };
            btn.Click += (s, e) => { result = dialogResult; dialog.Close(); };
            buttonPanel.Children.Add(btn);
        }

        switch (buttons)
        {
            case DialogButtons.OK:
                AddButton("OK", DialogResult.OK, true);
                break;
            case DialogButtons.OKCancel:
                AddButton("OK", DialogResult.OK, true);
                AddButton("Cancel", DialogResult.Cancel);
                break;
            case DialogButtons.YesNo:
                AddButton("Yes", DialogResult.Yes, true);
                AddButton("No", DialogResult.No);
                break;
            case DialogButtons.YesNoCancel:
                AddButton("Yes", DialogResult.Yes, true);
                AddButton("No", DialogResult.No);
                AddButton("Cancel", DialogResult.Cancel);
                break;
        }

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto")
        };
        Grid.SetRow(messageText, 0);
        Grid.SetRow(buttonPanel, 1);
        grid.Children.Add(messageText);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;

        var escapeResult = buttons switch
        {
            DialogButtons.OK => DialogResult.OK,
            DialogButtons.OKCancel => DialogResult.Cancel,
            DialogButtons.YesNo => DialogResult.No,
            DialogButtons.YesNoCancel => DialogResult.Cancel,
            _ => DialogResult.None,
        };
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                result = escapeResult;
                dialog.Close();
                e.Handled = true;
            }
        };

        await dialog.ShowDialog(parent);
        return result;
    }
}
