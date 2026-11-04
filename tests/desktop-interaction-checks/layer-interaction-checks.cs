using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Rendering;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

internal static partial class InteractionChecks
{
    /// <summary>
    /// Layer animation on the cobogó house in the real window: a key turns the effect on, the host builds the scene
    /// without the hidden level, and the video gets a layer source that does the same.
    /// </summary>
    private static void LayerAnimationChecks(MainWindow opened, RenderPanel panel)
    {
        var session = (DocumentSession)typeof(MainWindow).GetField("_session", PrivateInstance)!.GetValue(opened)!;
        Task<RenderScene?> SceneWithout(HashSet<Guid>? hidden) => (Task<RenderScene?>)(hidden is null
            ? typeof(MainWindow).GetMethod("GetSceneAsync", PrivateInstance, Type.EmptyTypes)!.Invoke(opened, null)!
            : typeof(MainWindow).GetMethod("GetSceneAsync", PrivateInstance, [typeof(IReadOnlySet<Guid>)])!.Invoke(opened, [hidden])!);
        var upper = session.Document.Levels.OrderBy(level => level.Elevation).Last().Id;
        var layers = (LayerAnimation)typeof(RenderPanel).GetField("_layers", PrivateInstance)!.GetValue(panel)!;
        var check = (CheckBox)panel.FindName("LayersAnimationCheck");

        Check(session.Document.Levels.Count >= 2, "The cobogó house has several levels to use as layers");
        Check(layers.IsEmpty && check.IsChecked != true, "A project starts with no layer animation");

        typeof(RenderPanel).GetMethod("SetLayerKey", PrivateInstance)!.Invoke(panel, [upper, 1.5f, true]);
        var active = (HashSet<string>)typeof(RenderPanel).GetField("_fxActive", PrivateInstance)!.GetValue(panel)!;
        Check(layers.Keys.Count == 1 && check.IsChecked == true && active.Contains("layers"),
            "Setting a layer key turns the effect on and puts it in the effect stack");

        var whole = Await(SceneWithout(null));
        var without = Await(SceneWithout(new HashSet<Guid> { upper }));
        Check(whole is not null && without is not null && without.TriangleCount < whole.TriangleCount,
            $"The host builds the scene without the hidden level ({without?.TriangleCount} < {whole?.TriangleCount})");
        Check(ReferenceEquals(Await(SceneWithout(new HashSet<Guid>())), whole),
            "With no level hidden the host gives the cached scene back");
        Check(session.Document.Walls.Any(wall => wall.LevelId == upper), "Building the scene without a level leaves the project untouched");

        var forRender = typeof(RenderPanel).GetMethod("LayersForRender", PrivateInstance)!.Invoke(panel, null) as LayerSource;
        Check(forRender is not null && forRender.Animation.HiddenAt(0).Contains(upper) && !forRender.Animation.HiddenAt(2).Contains(upper),
            "The video gets a copy of the layer animation to follow");

        check.IsChecked = false;
        Check(typeof(RenderPanel).GetMethod("LayersForRender", PrivateInstance)!.Invoke(panel, null) is null,
            "Switching the effect off makes the video ignore the keys");
        layers.Clear();
    }

    private static T Await<T>(Task<T> task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) Pump();
        return task.IsCompletedSuccessfully ? task.Result : throw new TimeoutException("The scene was not built in time.");
    }
}
