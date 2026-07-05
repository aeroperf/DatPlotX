using Avalonia.Controls;
using Avalonia.VisualTree;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Views;

/// <summary>
/// Coordinates event wiring and synchronization between plot panes.
/// Handles event subscriptions, X-axis synchronization, and cross-pane coordination.
/// </summary>
public class MainWindowEventCoordinator
{
    private bool _isSynchronizingXAxis;

    /// <summary>
    /// Wire up a PlotPaneControl when it loads - subscribe to all necessary events.
    /// Returns an IDisposable that, when disposed, detaches every subscription added here.
    /// </summary>
    public IDisposable WirePaneControl(
        PlotPaneControl paneControl,
        PlotPaneViewModel paneViewModel,
        Action<PlotPaneControl, double, double> onXAxisChanged,
        Action<double, double, double, bool> onMousePositionChanged,
        Action onPlotUpdated,
        Action<int> addCurvesToPaneHandler,
        Action<int> formatCurveHandler,
        Action<int> formatPaneHandler,
        Action<int> clearPaneHandler,
        Action addPaneHandler,
        Action<int> removePaneHandler,
        Action<PlotPaneViewModel, double> addEventLineAtPositionHandler,
        Action<PlotPaneViewModel> clearEventLinesHandler,
        Action<Guid> deleteEventLineHandler,
        Action<PlotPaneViewModel, Guid, double, double> calloutDragCompletedHandler,
        Action<PlotPaneViewModel, Guid, double> eventLineDragMovedHandler,
        Action<PlotPaneViewModel, Guid, double> eventLineDragCompletedHandler,
        Action<PlotPaneViewModel, double, double> addTextAnnotationHandler,
        Action<PlotPaneViewModel, Guid, double, double> textAnnotationDragCompletedHandler,
        Action<Guid> editTextAnnotationHandler,
        Action<Guid> deleteTextAnnotationHandler,
        Action<PlotPaneViewModel, double, double> addArrowAnnotationHandler,
        Action<PlotPaneViewModel, Guid, double, double, double, double> arrowAnnotationDragCompletedHandler,
        Action<Guid> editArrowAnnotationHandler,
        Action<Guid> deleteArrowAnnotationHandler,
        Action exportImageHandler)
    {
        paneControl.SetViewModel(paneViewModel);

        var unsubscribe = new List<Action>();

        Action<double, double> xAxisChanged = (min, max) => onXAxisChanged(paneControl, min, max);
        paneControl.XAxisChanged += xAxisChanged;
        unsubscribe.Add(() => paneControl.XAxisChanged -= xAxisChanged);

        Action<double, double, double, bool> mousePos = (x, y1, y2, hasY2) => onMousePositionChanged(x, y1, y2, hasY2);
        paneControl.MousePositionChanged += mousePos;
        unsubscribe.Add(() => paneControl.MousePositionChanged -= mousePos);

        paneViewModel.OnPlotUpdated += onPlotUpdated;
        unsubscribe.Add(() => paneViewModel.OnPlotUpdated -= onPlotUpdated);

        Action addCurves = () => addCurvesToPaneHandler(paneViewModel.PaneModel.Index);
        paneControl.AddCurvesToPaneRequested += addCurves;
        unsubscribe.Add(() => paneControl.AddCurvesToPaneRequested -= addCurves);

        Action formatCurve = () => formatCurveHandler(paneViewModel.PaneModel.Index);
        paneControl.FormatCurveRequested += formatCurve;
        unsubscribe.Add(() => paneControl.FormatCurveRequested -= formatCurve);

        Action formatPane = () => formatPaneHandler(paneViewModel.PaneModel.Index);
        paneControl.FormatPaneRequested += formatPane;
        unsubscribe.Add(() => paneControl.FormatPaneRequested -= formatPane);

        Action clearPane = () => clearPaneHandler(paneViewModel.PaneModel.Index);
        paneControl.ClearPaneRequested += clearPane;
        unsubscribe.Add(() => paneControl.ClearPaneRequested -= clearPane);

        paneControl.AddPaneRequested += addPaneHandler;
        unsubscribe.Add(() => paneControl.AddPaneRequested -= addPaneHandler);

        Action removePane = () => removePaneHandler(paneViewModel.PaneModel.Index);
        paneControl.RemovePaneRequested += removePane;
        unsubscribe.Add(() => paneControl.RemovePaneRequested -= removePane);

        Action<double> addEvtLine = (xPosition) => addEventLineAtPositionHandler(paneViewModel, xPosition);
        paneControl.AddEventLineAtPositionRequested += addEvtLine;
        unsubscribe.Add(() => paneControl.AddEventLineAtPositionRequested -= addEvtLine);

        Action clearEvtLines = () => clearEventLinesHandler(paneViewModel);
        paneControl.ClearEventLinesRequested += clearEvtLines;
        unsubscribe.Add(() => paneControl.ClearEventLinesRequested -= clearEvtLines);

        paneControl.DeleteEventLineRequested += deleteEventLineHandler;
        unsubscribe.Add(() => paneControl.DeleteEventLineRequested -= deleteEventLineHandler);

        Action<Guid, double, double> calloutDrag = (calloutId, offsetX, offsetY) =>
            calloutDragCompletedHandler(paneViewModel, calloutId, offsetX, offsetY);
        paneControl.CalloutDragCompleted += calloutDrag;
        unsubscribe.Add(() => paneControl.CalloutDragCompleted -= calloutDrag);

        Action<Guid, double> evtLineDragMoved = (eventLineId, newXPosition) =>
            eventLineDragMovedHandler(paneViewModel, eventLineId, newXPosition);
        paneControl.EventLineDragMoved += evtLineDragMoved;
        unsubscribe.Add(() => paneControl.EventLineDragMoved -= evtLineDragMoved);

        Action<Guid, double> evtLineDrag = (eventLineId, newXPosition) =>
            eventLineDragCompletedHandler(paneViewModel, eventLineId, newXPosition);
        paneControl.EventLineDragCompleted += evtLineDrag;
        unsubscribe.Add(() => paneControl.EventLineDragCompleted -= evtLineDrag);

        Action<double, double> addTextAnno = (x, y) => addTextAnnotationHandler(paneViewModel, x, y);
        paneControl.AddTextAnnotationRequested += addTextAnno;
        unsubscribe.Add(() => paneControl.AddTextAnnotationRequested -= addTextAnno);

        Action<Guid, double, double> textAnnoDrag = (annotationId, newX, newY) =>
            textAnnotationDragCompletedHandler(paneViewModel, annotationId, newX, newY);
        paneControl.TextAnnotationDragCompleted += textAnnoDrag;
        unsubscribe.Add(() => paneControl.TextAnnotationDragCompleted -= textAnnoDrag);

        paneControl.EditTextAnnotationRequested += editTextAnnotationHandler;
        unsubscribe.Add(() => paneControl.EditTextAnnotationRequested -= editTextAnnotationHandler);

        paneControl.DeleteTextAnnotationRequested += deleteTextAnnotationHandler;
        unsubscribe.Add(() => paneControl.DeleteTextAnnotationRequested -= deleteTextAnnotationHandler);

        Action<double, double> addArrowAnno = (x, y) => addArrowAnnotationHandler(paneViewModel, x, y);
        paneControl.AddArrowAnnotationRequested += addArrowAnno;
        unsubscribe.Add(() => paneControl.AddArrowAnnotationRequested -= addArrowAnno);

        Action<Guid, double, double, double, double> arrowAnnoDrag = (annotationId, baseX, baseY, tipX, tipY) =>
            arrowAnnotationDragCompletedHandler(paneViewModel, annotationId, baseX, baseY, tipX, tipY);
        paneControl.ArrowAnnotationDragCompleted += arrowAnnoDrag;
        unsubscribe.Add(() => paneControl.ArrowAnnotationDragCompleted -= arrowAnnoDrag);

        paneControl.EditArrowAnnotationRequested += editArrowAnnotationHandler;
        unsubscribe.Add(() => paneControl.EditArrowAnnotationRequested -= editArrowAnnotationHandler);

        paneControl.DeleteArrowAnnotationRequested += deleteArrowAnnotationHandler;
        unsubscribe.Add(() => paneControl.DeleteArrowAnnotationRequested -= deleteArrowAnnotationHandler);

        paneControl.ExportImageRequested += exportImageHandler;
        unsubscribe.Add(() => paneControl.ExportImageRequested -= exportImageHandler);

        return new DisposableAction(() =>
        {
            foreach (var a in unsubscribe) a();
            unsubscribe.Clear();
        });
    }

    private sealed class DisposableAction : IDisposable
    {
        private Action? _action;
        public DisposableAction(Action action) => _action = action;
        public void Dispose()
        {
            _action?.Invoke();
            _action = null;
        }
    }

    /// <summary>
    /// Synchronize X-axis across all panes when one pane's X-axis changes
    /// </summary>
    public void SynchronizeXAxis(
        PlotPaneControl sourcePaneControl,
        double min,
        double max,
        ObservableCollection<PlotPaneViewModel> panes,
        ItemsControl panesContainer,
        bool xAxisSynchronized)
    {
        if (_isSynchronizingXAxis || !xAxisSynchronized)
            return;

        _isSynchronizingXAxis = true;

        try
        {
            // Find all PlotPaneControl instances via visual tree
            var paneControls = panesContainer.GetVisualDescendants()
                .OfType<PlotPaneControl>();

            foreach (var paneControl in paneControls)
            {
                if (paneControl != sourcePaneControl)
                {
                    var paneVm = paneControl.GetViewModel();
                    if (paneVm != null && paneVm.PaneModel.XAxisSynchronized)
                    {
                        paneVm.SetXAxisRange(min, max);
                    }
                }
            }
        }
        finally
        {
            _isSynchronizingXAxis = false;
        }
    }

    /// <summary>
    /// Synchronize X-axis decimal places across all panes when one pane's X-axis decimals change
    /// </summary>
    public void SynchronizeXAxisDecimals(
        int sourcePaneIndex,
        int xAxisDecimals,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        foreach (var pane in panes)
        {
            if (pane.PaneModel.Index != sourcePaneIndex)
            {
                pane.PaneModel.XAxisDecimalPlaces = xAxisDecimals;
                pane.ApplyFormatting();
            }
        }
    }
}
