using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Navigation;
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

    /// <summary>
    /// The project's site in the real window: its latitude fills the Render panel's latitude box, and its turn from true
    /// north turns the sun the render is lit with.
    /// </summary>
    private static void SiteSunChecks(MainWindow opened, RenderPanel panel)
    {
        var session = (DocumentSession)typeof(MainWindow).GetField("_session", PrivateInstance)!.GetValue(opened)!;
        var original = session.Document.Site;
        var latitude = (TextBox)panel.FindName("LatitudeInput");
        var picker = (DatePicker)panel.FindName("SunDatePicker");
        var hour = (Slider)panel.FindName("HourSlider");
        RenderSettings Lit() => (RenderSettings)typeof(RenderPanel).GetMethod("LightingFor", PrivateInstance)!
            .Invoke(panel, [RenderQuality.Draft, ResourceMode.Eco])!;
        var scenario = (LightingScenario)typeof(RenderPanel).GetMethod("Scenario", PrivateInstance)!.Invoke(panel, null)!;
        var day = (picker.SelectedDate ?? DateTime.Today).DayOfYear;
        try
        {
            session.SetProjectSite(original with { Latitude = 40, TrueNorthDegrees = 0 });
            typeof(RenderPanel).GetMethod("SyncProjectSite", PrivateInstance)!.Invoke(panel, null);
            Check(latitude.Text == "40", "A change of the project's latitude fills the Render panel's latitude box");
            var facingNorth = Lit().SunDirection;
            Check(facingNorth == RenderPresets.Lighting(scenario, 40, day, hour.Value).SunDirection,
                "A project facing true north is lit by the plain solar position");

            session.SetProjectSite(original with { Latitude = 40, TrueNorthDegrees = 90 });
            typeof(RenderPanel).GetMethod("SyncProjectSite", PrivateInstance)!.Invoke(panel, null);
            var turned = Lit().SunDirection;
            var expected = SolarPosition.ToProject(facingNorth, 90);
            Check(Math.Abs(turned.X - expected.X) < 1e-4 && Math.Abs(turned.Y - expected.Y) < 1e-4 && Math.Abs(turned.Z - expected.Z) < 1e-4 &&
                  (Math.Abs(turned.X - facingNorth.X) > 0.05f || Math.Abs(turned.Y - facingNorth.Y) > 0.05f),
                "Turning the project from true north turns the render's sun with it");
        }
        finally
        {
            session.SetProjectSite(original);
            typeof(RenderPanel).GetMethod("SyncProjectSite", PrivateInstance)!.Invoke(panel, null);
        }
    }

    /// <summary>
    /// The movie in the real window: saving puts it in the project without rebuilding the 3D scene, and restoring brings
    /// the marks and layer keys back, as opening the project does.
    /// </summary>
    private static void MovieStorageChecks(MainWindow opened, RenderPanel panel)
    {
        var session = (DocumentSession)typeof(MainWindow).GetField("_session", PrivateInstance)!.GetValue(opened)!;
        var path = (CameraPath)typeof(RenderPanel).GetField("_path", PrivateInstance)!.GetValue(panel)!;
        var layers = (LayerAnimation)typeof(RenderPanel).GetField("_layers", PrivateInstance)!.GetValue(panel)!;
        var upper = session.Document.Levels.OrderBy(level => level.Elevation).Last().Id;
        var sceneVersion = typeof(MainWindow).GetField("_sceneVersion", PrivateInstance)!;
        var save = typeof(RenderPanel).GetMethod("SaveMovieNow", PrivateInstance)!;
        var restore = typeof(RenderPanel).GetMethod("RestoreMovie", PrivateInstance)!;

        path.Clear();
        path.Add(new CameraMark(new System.Numerics.Vector3(1, 2, 3), new System.Numerics.Vector3(4, 5, 6), 50, 0, "A"));
        path.Add(new CameraMark(new System.Numerics.Vector3(7, 8, 9), new System.Numerics.Vector3(4, 5, 6), 50, 3, "B"));
        layers.Clear();
        layers.Set(upper, 1.5f, true);
        var before = (int)sceneVersion.GetValue(opened)!;
        save.Invoke(panel, null);
        Check(session.GetExtension("render.movie") is { Length: > 20 } && (int)sceneVersion.GetValue(opened)! == before,
            "Saving the movie keeps it in the project without rebuilding the 3D scene");

        path.Clear();
        layers.Clear();
        restore.Invoke(panel, null);
        Check(path.Marks.Count == 2 && path.Marks[1].SecondsFromPrevious == 3 && path.Marks[0].Name == "A" &&
              layers.Keys.Count == 1 && layers.Keys[0].LevelId == upper &&
              ((CheckBox)panel.FindName("LayersAnimationCheck")).IsChecked == true,
            "Opening a project restores its camera marks and layer keys and switches layer animation on");

        session.SetExtension("render.movie", null);
        restore.Invoke(panel, null);
        Check(path.Marks.Count == 0 && layers.IsEmpty, "A project without a movie opens with a blank one, not the previous project's");
    }

    /// <summary>
    /// Every effect of the picker edits a control that really exists in the Render panel, with a range the effect's own
    /// starting value and neutral value fit in, so adding or muting an effect can never silently do nothing.
    /// </summary>
    private static void EffectControlChecks(RenderPanel panel)
    {
        var problems = new List<string>();
        foreach (var effect in RenderFx.All)
            foreach (var control in effect.Controls)
            {
                switch (panel.FindName(control.Key))
                {
                    case Slider slider:
                        if (control.Kind != FxControlKind.Slider || control.Neutral < slider.Minimum || control.Neutral > slider.Maximum ||
                            control.StartValue < slider.Minimum || control.StartValue > slider.Maximum)
                            problems.Add($"{effect.Id}/{control.Key} (range)");
                        break;
                    case CheckBox when control.Kind == FxControlKind.Toggle:
                    case ComboBox when control.Kind == FxControlKind.Choice:
                        break;
                    default:
                        problems.Add($"{effect.Id}/{control.Key} (missing)");
                        break;
                }
            }
        Check(problems.Count == 0 && RenderFx.All.Count >= 20, "Every control of every effect exists in the Render panel and its range holds the effect's start and neutral values" +
                                                                (problems.Count == 0 ? "" : ": " + string.Join(", ", problems)));
    }

    /// <summary>
    /// Decals, the ocean and the people in the real window, sharing ONE rebuild of the house's render scene (2.6 million
    /// triangles, ten seconds or more each): all three are saved first, the scene is built once, and what it holds is
    /// checked together. The panel's own controls (ocean save and restore) and the texts kept in the project are checked
    /// without a scene, and the removals are checked in the project alone.
    /// </summary>
    private static void RenderExtrasChecks(MainWindow opened, RenderPanel panel)
    {
        var session = (DocumentSession)typeof(MainWindow).GetField("_session", PrivateInstance)!.GetValue(opened)!;
        Task<RenderScene?> Scene() => (Task<RenderScene?>)typeof(MainWindow).GetMethod("GetSceneAsync", PrivateInstance, Type.EmptyTypes)!.Invoke(opened, null)!;
        var host = typeof(RenderPanel).GetField("_source", PrivateInstance)!.GetValue(panel)!;
        var saveDecals = typeof(MainWindow).GetMethod("SaveDecals", PrivateInstance)!;
        var savePeople = host.GetType().GetMethod("SetActors")!;
        var readPeople = host.GetType().GetMethod("GetActors")!;
        var saveFires = host.GetType().GetMethod("SetFires")!;
        var readFires = host.GetType().GetMethod("GetFires")!;
        var saveOcean = typeof(RenderPanel).GetMethod("SaveOceanNow", PrivateInstance)!;
        var restoreOcean = typeof(RenderPanel).GetMethod("RestoreOcean", PrivateInstance)!;
        var on = (CheckBox)panel.FindName("OceanCheck");
        var level = (Slider)panel.FindName("OceanLevelSlider");
        var waves = (Slider)panel.FindName("OceanWavesSlider");
        var tint = (ComboBox)panel.FindName("OceanTintPicker");
        bool HasSea(RenderScene scene) => scene.Materials.Any(material => material.Pattern == SurfacePattern.Waves);
        var image = Path.Combine(Path.GetTempPath(), $"g3d-decal-{Guid.NewGuid():N}.png");
        try
        {
            var pixels = new byte[4 * 4 * 4];
            for (var index = 0; index < pixels.Length; index += 4) { pixels[index] = 255; pixels[index + 2] = 255; pixels[index + 3] = 255; }
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
                4, 4, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 16)));
            using (var stream = File.Create(image)) encoder.Save(stream);

            // What the house starts with.
            var before = Await(Scene())!;
            Check(before.DynamicTriangleCount == 0 && !HasSea(before) && ((IReadOnlyList<Actor>)readPeople.Invoke(host, null)!).Count == 0 && tint.Items.Count == 3,
                "The house starts with no people and no ocean, and the colour list has its three waters");

            // Decals: one on the longest wall.
            var longest = session.Document.Walls.OrderByDescending(item => item.Length).First();
            var decal = new Decal(Guid.NewGuid(), longest.Id, DecalFace.Left, longest.Length / 2, longest.BaseOffset + 1.2,
                Math.Min(1, longest.Length / 2), 1, image);
            Check(Decals.Fit(longest, decal) is null, "A decal sized for the cobogó house's longest wall fits it");
            saveDecals.Invoke(opened, [new List<Decal> { decal }]);
            Check(session.GetExtension(Decals.Key) is { Length: > 20 }, "Saving decals keeps them in the project");

            // The ocean, through the panel's own controls.
            level.Value = -1.5;
            waves.Value = 2;
            tint.SelectedIndex = 1;
            on.IsChecked = true;
            saveOcean.Invoke(panel, null);
            Check(session.GetExtension(Ocean.Key) is { Length: > 10 } && Ocean.Of(session.Document) == new OceanSettings(true, -1.5, 2, OceanTint.Pacific),
                "Switching the ocean on keeps its level, wave size and colour in the project");
            level.Value = -0.5;
            waves.Value = 1;
            tint.SelectedIndex = 0;
            on.IsChecked = false;
            restoreOcean.Invoke(panel, null);
            Check(on.IsChecked == true && Math.Abs(level.Value + 1.5) < 1e-9 && Math.Abs(waves.Value - 2) < 1e-9 && tint.SelectedIndex == 1,
                "Opening a project shows its ocean in the panel's controls");

            // People: two, one walking a route and one standing.
            var start = new ActorPoint(longest.Start.X, longest.Start.Y - 2, 0);
            var people = new List<Actor>
            {
                new(Guid.NewGuid(), "Ana", 0, 1.2, true, 0, [start, start with { X = start.X + 5 }]),
                new(Guid.NewGuid(), "Luis", 3, 1.0, false, 2, [start with { Y = start.Y - 1 }]),
                new(Guid.NewGuid(), "Coche", 2, 8, true, 0, [start with { Y = start.Y - 6 }, start with { X = start.X + 12, Y = start.Y - 6 }], ActorKind.Car)
            };
            savePeople.Invoke(host, [people]);
            // Fires: a campfire (flames and smoke) and a column of smoke, near the people.
            var fires = new List<FireEmitter>
            {
                new(Guid.NewGuid(), "Fogata", start.X + 4, start.Y - 3, 0, 0.9, 0.5, 1.2),
                new(Guid.NewGuid(), "Humo", start.X + 8, start.Y - 3, 0, 0, 0.7, 1.0)
            };
            saveFires.Invoke(host, [fires]);
            Check(session.GetExtension(Fires.Key) is { Length: > 50 } && ((IReadOnlyList<FireEmitter>)readFires.Invoke(host, null)!).Count == 2,
                "Saving fires keeps them in the project");
            Check(session.GetExtension(Actors.Key) is { Length: > 50 } && ((IReadOnlyList<Actor>)readPeople.Invoke(host, null)!).Count == 3,
                "Saving people keeps them, with their routes, in the project");

            // ONE rebuild: the scene now has the sea, the decal and the people together.
            var after = Await(Scene())!;
            Check(HasSea(after), "Switching the ocean on puts the sea in the render scene");
            Check(after.DynamicTriangleCount > 30000 &&
                  after.TriangleCount == before.TriangleCount + 2 + after.DynamicTriangleCount,
                $"The render scene holds the decal's two triangles and the two people and the car as a block of moving triangles ({after.DynamicTriangleCount}) after everything else ({before.TriangleCount} -> {after.TriangleCount})");
            Check(after.Lights.Length == before.Lights.Length + 1,
                $"The campfire's flames light the render scene with one warm light, and a column of smoke adds none ({before.Lights.Length} -> {after.Lights.Length})");
            var tracer = new PathTracer(after, 16, 16, new RenderCamera(new System.Numerics.Vector3(0, -8, 2), System.Numerics.Vector3.Zero, 45));
            // The window's own loader of car meshes, as the program reads them from its resources folder.
            FamilyMesh.Decoded? CarMesh(int model) => NativeDocumentStore.LoadFamily(Path.Combine(AppContext.BaseDirectory, "resources", "families",
                (model == 0 ? "sedan-car" : "coupe-car-silver") + ".g3df")).Mesh!.Decode();
            var rig = ActorRig.From(people, CarMesh)!;
            var (movedPositions, movedNormals) = rig.Frame(3);
            tracer.MoveActors(movedPositions, movedNormals);
            Check(tracer.HasActors && rig.VertexCount == after.DynamicVertexCount, "A tracer of the house's scene can move the people frame by frame");

            // Removals, checked in the project (each would otherwise cost another rebuild).
            saveDecals.Invoke(opened, [new List<Decal>()]);
            savePeople.Invoke(host, [new List<Actor>()]);
            saveFires.Invoke(host, [new List<FireEmitter>()]);
            on.IsChecked = false;
            saveOcean.Invoke(panel, null);
            Check(!Ocean.Of(session.Document).Enabled && session.GetExtension(Ocean.Key) is not null,
                "Switching the ocean off keeps its level and colour in the project for next time");
            level.Value = -0.5;
            waves.Value = 1;
            tint.SelectedIndex = 0;
            saveOcean.Invoke(panel, null);
            Check(session.GetExtension(Decals.Key) is null && session.GetExtension(Actors.Key) is null && session.GetExtension(Fires.Key) is null && session.GetExtension(Ocean.Key) is null,
                "Removing the decals, people and fires, and an ocean back at its defaults, keep nothing in the project");
        }
        finally
        {
            if (File.Exists(image)) File.Delete(image);
        }
    }

    private static T Await<T>(Task<T> task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) Pump();
        return task.IsCompletedSuccessfully ? task.Result : throw new TimeoutException("The scene was not built in time.");
    }
}
