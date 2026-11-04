using System.Numerics;
using System.Windows;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>The "Personas" window of the Movie bar: people placed where the view looks, walking routes, drawn in the render and the video.</summary>
public partial class RenderPanel
{
    private PeopleDialog? _peopleWindow;

    /// <summary>Opens the window of people beside the view (one at a time).</summary>
    private void ShowPeopleWindow()
    {
        if (_source is null) return;
        if (_peopleWindow is { IsLoaded: true })
        {
            _peopleWindow.Activate();
            return;
        }
        var source = _source;
        _peopleWindow = new PeopleDialog(source.GetActors(), PointUnderView, actors =>
        {
            source.SetActors(actors);
            StatusText.Text = actors.Count == 0 ? "No hay personas ni coches en el proyecto." : $"{actors.Count} personas y coches en el proyecto; el render se actualiza.";
        })
        {
            Owner = Window.GetWindow(this)
        };
        var owner = _peopleWindow.Owner;
        if (owner is not null)
        {
            _peopleWindow.Left = owner.Left + Math.Max(0, owner.ActualWidth - 480);
            _peopleWindow.Top = owner.Top + 90;
        }
        _peopleWindow.Show();
    }

    /// <summary>
    /// The ground under the centre of the view: the point the view looks at, brought down to the first surface below it, so
    /// a person looked at on a wall or a tree still stands on the floor.
    /// </summary>
    private ActorPoint? PointUnderView()
    {
        if (_source is null) return null;
        var camera = _source.Camera;
        if (_source.Query?.Pick(camera.Position, camera.Forward) is not { } distance) return null;
        var hit = camera.Position + camera.Forward * distance;
        var above = hit + new Vector3(0, 0, 0.3f);
        var height = _source.Query.Pick(above, -Vector3.UnitZ) is { } drop ? above.Z - drop : hit.Z;
        return new ActorPoint(hit.X, hit.Y, height);
    }
}
