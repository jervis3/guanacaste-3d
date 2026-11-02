using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>
/// The photographic lens of the Camera tab: focal length in millimetres (a full-frame sensor, 24 mm tall) tied to the
/// field of view the navigation camera really uses, a tilted horizon, a shift lens and a manual focus distance.
/// </summary>
public partial class RenderPanel
{
    private static readonly int[] FocalPresetLengths = [14, 24, 35, 50, 85, 135];
    private bool _syncingLens;

    /// <summary>Builds the quick focal-length buttons once.</summary>
    private void InitializeLens()
    {
        FocalPresets.Children.Clear();
        foreach (var length in FocalPresetLengths)
        {
            var millimetres = length;
            var button = new Button
            {
                Content = $"{length} mm", Style = (Style)FindResource("PanelButton"), Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(9, 4, 9, 4), MinHeight = 28, MinWidth = 52
            };
            button.Click += (_, _) => FocalSlider.Value = millimetres;
            FocalPresets.Children.Add(button);
        }
        SyncLensFromCamera();
    }

    /// <summary>Shows the camera's current field of view as a focal length, without feeding it back to the camera.</summary>
    private void SyncLensFromCamera()
    {
        if (_source is null || FocalSlider is null) return;
        var focal = FocalLengthOf(_source.Camera.FieldOfViewDegrees);
        _syncingLens = true;
        try
        {
            if (Math.Abs(FocalSlider.Value - focal) > 0.01) FocalSlider.Value = Math.Clamp(focal, FocalSlider.Minimum, FocalSlider.Maximum);
            ShowLensTexts(focal, _source.Camera.FieldOfViewDegrees);
        }
        finally { _syncingLens = false; }
    }

    private static double FocalLengthOf(double verticalFieldOfView) =>
        PhotographicCamera.FocalLength(Math.Clamp(verticalFieldOfView, 5, 150));

    private void ShowLensTexts(double focal, double verticalFieldOfView)
    {
        FocalValueText.Text = $"{focal:0} mm";
        FieldOfViewText.Text = string.Format(CultureInfo.CurrentCulture, UiText.Get("RenderPanel.FieldOfViewFormat"),
            verticalFieldOfView);
        FocusValueText.Text = FocusSlider.Value <= 0 ? UiText.Get("RenderPanel.FocusAuto") : $"{FocusSlider.Value:0.#} m";
    }

    /// <summary>The slider changes the real field of view, so the navigation view, the preview and the render agree.</summary>
    private void FocalSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingLens || !_ready || _source is null || _navigator is null) return;
        var fieldOfView = PhotographicCamera.VerticalFieldOfView(FocalSlider.Value);
        var camera = _source.Camera;
        _navigator.ApplyCamera(camera.Position, camera.YawDegrees, camera.PitchDegrees, fieldOfView, recordHistory: false);
    }

    /// <summary>Roll, shift and focus change the picture, not the pose, so only the views are refreshed.</summary>
    private void Lens_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _source is null) return;
        if (_source.Camera is { } camera) ShowLensTexts(FocalSlider.Value, camera.FieldOfViewDegrees);
        OnCameraChanged();
    }

    private void ResetLens_Click(object sender, RoutedEventArgs e)
    {
        RollSlider.Value = 0;
        ShiftXSlider.Value = 0;
        ShiftYSlider.Value = 0;
        FocusSlider.Value = 0;
        FocalSlider.Value = FocalLengthOf(45);
    }
}
