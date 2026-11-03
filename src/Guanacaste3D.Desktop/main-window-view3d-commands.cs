using System.Windows;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private static readonly string[] SunPresetCycle = ["Off", "Morning", "Noon", "Afternoon"];

    /// <summary>Whether the 3D view uses parallel projection instead of perspective.</summary>
    private bool _modelOrthographic;

    /// <summary>
    /// Routes the View ribbon's 3D commands to the options of the model view that already exist in its toolbar and side
    /// panel: line weight, hidden lines, shadows, the sun, locking and the navigation bar. Each one first brings up the
    /// 3D view, because its result is only visible there.
    /// </summary>
    private bool TryExecuteView3DCommand(string commandId)
    {
        switch (commandId)
        {
            case "ThinLines":
            case "ShowHiddenLines":
            case "RemoveHiddenLines":
            case "Shadows":
            case "SunPath":
            case "LockView":
            case "Orbit":
            case "Pan":
            case "Zoom":
            case "ViewCube":
            case "Perspective":
            case "Orthographic":
                break;
            default:
                return false;
        }

        if (!_tileViews) ViewTabs.SelectedIndex = Model3DTabIndex;
        switch (commandId)
        {
            case "ThinLines":
                // The side panel's checkbox owns the setting, so the panel, the toolbar and the saved view agree.
                ThickLinesCheck.IsChecked = ThickLinesCheck.IsChecked != true;
                StatusText.Text = GetText(ThickLinesCheck.IsChecked == true ? "Status.Lines3DThick" : "Status.Lines3DThin");
                break;
            case "ShowHiddenLines":
                ExecuteModelViewCommand("Style.Wireframe");
                StatusText.Text = GetText("Status.Style3DWireframe");
                break;
            case "RemoveHiddenLines":
                ExecuteModelViewCommand("Style.HiddenLine");
                StatusText.Text = GetText("Status.Style3DHiddenLine");
                break;
            case "Shadows":
                ExecuteModelViewCommand("Shadows");
                StatusText.Text = GetText(_modelShadows ? "Status.Shadows3DOn" : "Status.Shadows3DOff");
                break;
            case "SunPath":
                var next = SunPresetCycle[(Array.IndexOf(SunPresetCycle, _modelSunPreset) + 1) % SunPresetCycle.Length];
                ExecuteModelViewCommand("Sun." + next);
                StatusText.Text = string.Format(GetText("Status.Sun3D"), GetText("SunPreset." + next));
                break;
            case "LockView":
                ExecuteModelViewCommand("Lock");
                StatusText.Text = GetText(_modelNavigator?.IsLocked == true ? "Status.View3DLocked" : "Status.View3DUnlocked");
                break;
            case "Perspective":
            case "Orthographic":
                _modelOrthographic = commandId == "Orthographic";
                UpdateModelCamera();
                StatusText.Text = GetText(_modelOrthographic ? "Status.Projection3DOrthographic" : "Status.Projection3DPerspective");
                break;
            default:
                // Orbit, pan, zoom and the view cube are done with the pointer; the bar brings them within reach.
                ExecuteModelViewCommand("NavigationBar");
                StatusText.Text = GetText("Status.Navigate3D");
                break;
        }
        return true;
    }
}
