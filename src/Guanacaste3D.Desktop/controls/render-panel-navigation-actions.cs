using System.Windows;

namespace Guanacaste3D.Desktop.Controls;

public partial class RenderPanel
{
    /// <summary>Rotates around the selected element or project center using the shared model camera.</summary>
    private void OrbitLeft_Click(object sender, RoutedEventArgs e) => _navigator?.OrbitBy(-80, 0);

    /// <summary>Rotates around the selected element or project center using the shared model camera.</summary>
    private void OrbitRight_Click(object sender, RoutedEventArgs e) => _navigator?.OrbitBy(80, 0);

    /// <summary>Recovers a view of the whole project when the current camera is far from the model.</summary>
    private void FrameModel_Click(object sender, RoutedEventArgs e) => _navigator?.FrameAll();
}
