using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

internal static partial class InteractionChecks
{
    /// <summary>
    /// Opens the breeze-block (cobogó) house from the Home screen, the way a user does, and works on it: every level is
    /// drawn, every wall is selected and shown in the properties panel, every kind of element is found by identifier,
    /// categories are hidden and shown again, and random sequences of real ribbon commands run on its real walls.
    /// Nothing here uses a layout made up for the test, so the checks meet the joins, tees, openings and levels of an
    /// actual design. The window is its own, so the rest of the suite is untouched.
    /// </summary>
    private static void CobogoHouseChecks()
    {
        var previous = _window;
        var opened = new MainWindow(showStartScreen: true) { Width = 1440, Height = 900, WindowState = WindowState.Normal };
        _window = opened;
        try
        {
            opened.Show();
            Pump();
            var host = (IStartScreenHost)opened;
            host.OpenSample(host.Samples.Single(sample => sample.Id == "cobogo"));
            Pump();
            _canvas = (Canvas)opened.FindName("PlanCanvas");
            var session = Field<DocumentSession>("_session");
            var initial = ProjectFile.SerializeDocument(session.Document);
            var tabs = (TabControl)opened.FindName("ViewTabs");
            var properties = (TextBlock)opened.FindName("PropertiesText");

            Check(session.Document.Walls.Count >= 20 && session.Document.Openings.Count >= 3 && session.Document.Levels.Count >= 2,
                $"The cobogó house opens with its real design: {session.Document.Walls.Count} walls, " +
                $"{session.Document.Openings.Count} openings, {session.Document.Levels.Count} levels");

            // Every level is drawn in plan, and every wall can be selected and described in the properties panel.
            tabs.SelectedIndex = 0;
            var described = 0;
            foreach (var level in session.Document.Levels)
            {
                var walls = session.Document.Walls.Where(wall => wall.LevelId == level.Id).ToArray();
                if (walls.Length == 0) continue;
                Check((bool)Invoke("SelectElement", walls[0].Id)!, $"Level {level.Name} is reached by selecting one of its walls");
                Invoke("RenderPlan");
                Pump();
                Check(_canvas.Children.Count > walls.Length, $"Plan of {level.Name} draws its {walls.Length} walls and more");
                foreach (var wall in walls)
                {
                    SetField("_selectedWallId", (Guid?)wall.Id);
                    Invoke("RefreshProperties");
                    Invoke("RenderPlan");
                    if (!string.IsNullOrWhiteSpace(properties.Text)) described++;
                }
            }
            Check(described >= session.Document.Walls.Count - 2,
                $"The properties panel describes {described} of the house's {session.Document.Walls.Count} walls");

            // Every kind of element is found by identifier and selected.
            var document = session.Document;
            var firstOfEach = new (string Name, Guid? Id, string Field)[]
            {
                ("a wall", document.Walls.FirstOrDefault()?.Id, "_selectedWallId"),
                ("an opening", document.Openings.FirstOrDefault()?.Id, "_selectedOpeningId"),
                ("a room", document.Rooms.FirstOrDefault()?.Id, "_selectedRoomId"),
                ("a floor", document.Floors.FirstOrDefault()?.Id, "_selectedFloorId"),
                ("a column", document.Columns.FirstOrDefault()?.Id, "_selectedColumnId"),
                ("a structural element", document.StructuralElements.FirstOrDefault()?.Id, "_selectedStructuralId")
            };
            foreach (var (name, id, field) in firstOfEach.Where(entry => entry.Id is not null))
            {
                Check((bool)Invoke("SelectElement", id!.Value)! && Field<Guid?>(field) == id,
                    $"Select by identifier finds {name} of the house");
            }
            Check(!(bool)Invoke("SelectElement", Guid.NewGuid())!, "Select by identifier reports an unknown identifier");

            // The 3D view switches between perspective and parallel projection with a usable camera either way.
            tabs.SelectedIndex = 1;
            Pump();
            var viewport = (System.Windows.Controls.Viewport3D)opened.FindName("ModelViewport");
            Invoke("ExecuteApplicationCommand", "Orthographic");
            Pump();
            var parallel = viewport.Camera as System.Windows.Media.Media3D.OrthographicCamera;
            Check(parallel is not null && double.IsFinite(parallel.Width) && parallel.Width > 1 && parallel.NearPlaneDistance < 0,
                $"Orthographic gives the 3D view a parallel camera of a usable width ({parallel?.Width:0.0} m)");
            Invoke("ExecuteApplicationCommand", "Perspective");
            Pump();
            Check(viewport.Camera is System.Windows.Media.Media3D.PerspectiveCamera, "Perspective brings the perspective camera back");
            tabs.SelectedIndex = 0;
            Pump();

            // Elevation and section are drawn when their tabs open, not on every edit, and they show the current model.
            var elevationCanvas = (Canvas)opened.FindName("ElevationCanvas");
            tabs.SelectedIndex = 4;
            Pump();
            Check(elevationCanvas.Children.Count > 20, $"The elevation tab draws the house when it opens ({elevationCanvas.Children.Count} shapes)");
            var sectionCanvas = (Canvas)opened.FindName("SectionCanvas");
            tabs.SelectedIndex = 5;
            Pump();
            Check(sectionCanvas.Children.Count > 20, $"The section tab draws the house when it opens ({sectionCanvas.Children.Count} shapes)");
            var sheetCanvas = (Canvas)opened.FindName("SheetCanvas");
            tabs.SelectedIndex = 3;
            Pump();
            Check(sheetCanvas.Children.Count > 10, $"The sheets tab draws the house's sheet when it opens ({sheetCanvas.Children.Count} layers)");
            tabs.SelectedIndex = 0;
            Pump();

            // Hiding a category thins the drawing and showing it again restores it.
            var groundWall = session.Document.Walls[0];
            Invoke("SelectElement", groundWall.Id);
            Invoke("RenderPlan");
            Pump();
            var planView = session.Document.Views.First(view => view.Kind == ProjectViewKind.Plan && view.LevelId == groundWall.LevelId);
            var full = _canvas.Children.Count;
            session.SetViewHiddenCategories(planView.Id, [ViewCategory.Walls, ViewCategory.Doors, ViewCategory.Windows]);
            Invoke("RenderPlan");
            Pump();
            var thinned = _canvas.Children.Count;
            session.SetViewHiddenCategories(planView.Id, []);
            Invoke("RenderPlan");
            Pump();
            Check(thinned < full && _canvas.Children.Count == full,
                $"Hiding walls, doors and windows thins the house's plan ({full} to {thinned} shapes) and showing them restores it");

            // The warnings of a real design are reviewable, and none of them is a crash or a duplicate.
            var warnings = ModelWarnings.Analyze(session.Document);
            Check(warnings.All(warning => warning.ElementIds.Count > 0) &&
                  warnings.All(warning => warning.Code != "DuplicateWalls"),
                $"Reviewing the house's warnings works ({warnings.Count} found: {string.Join(", ", warnings.Select(warning => warning.Code).Distinct())})");

            if (Environment.GetEnvironmentVariable("G3D_PROFILE") == "1") ProfileRefresh();

            // Each ribbon command does what it says on the longest real wall of the house.
            var longest = session.Document.Walls.Where(wall => !wall.Pinned).OrderByDescending(wall => wall.Length).First();
            Wall Current() => session.Document.Walls.Single(wall => wall.Id == longest.Id);
            Invoke("SelectElement", longest.Id);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Invoke("ExecuteApplicationCommand", "DisallowJoin");
            Console.WriteLine($"INFO: one ribbon command on the cobogó house takes {timer.ElapsedMilliseconds} ms, almost all of it " +
                              "redrawing the plan and the 3D view (the model itself changes in about 0.1 s)");
            Check(Current().StartJoin == WallJoinSetting.Disallowed && Current().EndJoin == WallJoinSetting.Disallowed,
                "Disallow Join releases both ends of the house's longest wall");
            session.SetWallJoins([longest.Id], WallJoinSetting.Butt);
            Invoke("RefreshAll");
            Invoke("ExecuteApplicationCommand", "ButtJoin");
            Check(Current().StartJoin == WallJoinSetting.Butt, "Butt Join makes the wall butt");
            Invoke("ExecuteApplicationCommand", "AllowJoin");
            Check(Current().StartJoin == WallJoinSetting.Miter && Current().EndJoin == WallJoinSetting.Miter,
                "Allow Join restores mitered corners");
            Invoke("ExecuteApplicationCommand", "Pin");
            Check(Current().Pinned, "Pin pins the wall");
            Invoke("ExecuteApplicationCommand", "Pin");
            Check(!Current().Pinned, "Pin toggles off again");
            var start = Current().Start;
            var hosted = session.Document.Openings.Count(opening => opening.WallId == longest.Id);
            Invoke("ExecuteApplicationCommand", "FlipWall");
            Check(Current().End == start, "Flip Wall swaps the ends of the wall");
            Invoke("ExecuteApplicationCommand", "FlipWall");
            Check(Current().Start == start, "Flipping again restores the wall");
            var wallCount = session.Document.Walls.Count;
            var openingCount = session.Document.Openings.Count;
            Invoke("ExecuteApplicationCommand", "Copy");
            Invoke("ExecuteApplicationCommand", "Paste");
            Check(session.Document.Walls.Count == wallCount + 1 && session.Document.Openings.Count == openingCount + hosted,
                $"Copy and Paste add a wall with its {hosted} openings");
            Invoke("ExecuteApplicationCommand", "Cut");
            Check(session.Document.Walls.Count == wallCount && session.Document.Openings.Count == openingCount,
                "Cut removes the pasted wall with its openings");

            // Commands that ask for numbers or text, answered the way a person would, on the house's real walls.
            var responder = typeof(MainWindow).Assembly.GetType("Guanacaste3D.Desktop.TextPrompt")!
                .GetField("Responder", BindingFlags.NonPublic | BindingFlags.Static)!;
            void Answer(params string?[] replies)
            {
                var queue = new Queue<string?>(replies);
                responder.SetValue(null, (Func<string, string?>)(_ => queue.Count > 0 ? queue.Dequeue() : null));
            }
            try
            {
                Invoke("SelectElement", longest.Id);
                var count = session.Document.Walls.Count;
                Answer("3; 0; 2");
                Invoke("ExecuteApplicationCommand", "Array");
                Check(session.Document.Walls.Count == count + 2, "The Array command makes a linear array of three from the house's longest wall");
                Invoke("SelectElement", longest.Id);
                Answer("r; 4; 90");
                Invoke("ExecuteApplicationCommand", "Array");
                Check(session.Document.Walls.Count == count + 5, "The Array command also makes a radial array of four");
                Invoke("SelectElement", longest.Id);
                Answer("0.5");
                Invoke("ExecuteApplicationCommand", "Offset");
                Check(session.Document.Walls.Count == count + 6, "The Offset command copies the wall half a meter to its left");
                Answer("zzz-no-existe", "x");
                Invoke("ExecuteApplicationCommand", "FindReplace");
                Check(!string.IsNullOrWhiteSpace(((TextBlock)opened.FindName("StatusText")).Text), "Find and Replace reports when the text is nowhere");
                var room = session.Document.Rooms[0].Name.Split(' ')[0];
                Answer(room, "ZONA");
                Invoke("ExecuteApplicationCommand", "FindReplace");
                Check(session.Document.Rooms.Any(candidate => candidate.Name.Contains("ZONA")), $"Find and Replace changes '{room}' in the house's rooms");
                Invoke("ExecuteApplicationCommand", "Numbering");
                Check(session.Document.Rooms.Select(candidate => candidate.Number).Distinct().Count() == session.Document.Rooms.Count,
                    "The Numbering command leaves every room with its own number");
                Invoke("SelectElement", longest.Id);
                Answer("1.05");
                Invoke("ExecuteApplicationCommand", "Scale");
                Invoke("ExecuteApplicationCommand", "AttachTop");
                Check(session.Document.Walls.Any(), "Scale and Attach Top run on a real wall without failing");
            }
            finally
            {
                responder.SetValue(null, null);
            }

            CrashProtectionChecks(opened, session);

            RandomRibbonOnHouse(session);

            // Undoing everything the window did returns the house to what was opened, whatever the commands were.
            var guard = 0;
            while (session.CanUndo && guard++ < 10000) session.Undo();
            Check(ProjectFile.SerializeDocument(session.Document) == initial,
                "Undoing every command of the session brings the cobogó house back exactly as it opened");
        }
        finally
        {
            // This throwaway window intentionally mutates the sample. Undo restores the document,
            // but does not clear the unsaved-changes flag; closing with it set opens a modal
            // confirmation and hangs unattended Windows CI until its ten-minute timeout.
            SetField("_dirty", false);
            opened.Close();
            _window = previous;
            if (previous is not null) _canvas = (Canvas)previous.FindName("PlanCanvas");
        }
    }

    /// <summary>
    /// What protects the work when the program stops unexpectedly, on the real house: a change is copied on its own, in
    /// the background; the copy is the project to the last byte; a new session offers it and recovers it; accepting
    /// keeps it until the project is saved; closing on purpose leaves nothing behind; an error is logged.
    /// </summary>
    private static void CrashProtectionChecks(MainWindow opened, DocumentSession session)
    {
        var recovery = Environment.GetEnvironmentVariable("G3D_RECOVERY_DIR")!;
        var id = session.Document.Id;

        // The deterministic commands above left the project changed, so an emergency save writes its copy at once.
        Invoke("EmergencySave");
        var copy = AutoRecovery.Find(recovery).SingleOrDefault(candidate => candidate.DocumentId == id);
        Check(copy is not null && ProjectFile.SerializeDocument(AutoRecovery.Load(copy)) == ProjectFile.SerializeDocument(session.Document),
            "An emergency save writes a copy of the house identical to the project being edited");

        // A further change is copied by itself, in the background, without anyone asking.
        var savedBefore = copy!.SavedAtUtc;
        var wall = session.Document.Walls.First(candidate => !candidate.Pinned);
        session.SetWallsPinned([wall.Id], true);
        var current = ProjectFile.SerializeDocument(session.Document);
        var waited = System.Diagnostics.Stopwatch.StartNew();
        var copiedByItself = false;
        while (waited.Elapsed < TimeSpan.FromSeconds(60) && !copiedByItself)
        {
            Pump();
            System.Threading.Thread.Sleep(300);
            var latest = AutoRecovery.Find(recovery).SingleOrDefault(candidate => candidate.DocumentId == id);
            copiedByItself = latest is not null && latest.SavedAtUtc > savedBefore &&
                             ProjectFile.SerializeDocument(AutoRecovery.Load(latest)) == current;
        }
        Check(copiedByItself, $"A change to the house is copied automatically within {waited.Elapsed.TotalSeconds:0} s, with nobody asking");

        // A new session finds the copy, offers it and recovers the project exactly, still marked as unsaved.
        var asked = new List<string>();
        var second = new MainWindow { Width = 800, Height = 600 };
        try
        {
            second.Show();
            SetInstanceField(second, "_recoveryPromptSink", (Func<string, bool>)(question => { asked.Add(question); return true; }));
            Instance(second, "OfferRecovery");
            var recovered = InstanceField<DocumentSession>(second, "_session").Document;
            Check(asked.Count == 1 && asked[0].Contains(session.Document.Name) &&
                  ProjectFile.SerializeDocument(recovered) == current && InstanceField<bool>(second, "_dirty"),
                "A new session offers the leftover copy and recovers the house exactly, still unsaved");
            Check(AutoRecovery.Find(recovery).Any(candidate => candidate.DocumentId == id),
                "The copy stays until the recovered project is saved");
            SetInstanceField(second, "_dirty", false);
        }
        finally
        {
            second.Close();
        }
        Check(AutoRecovery.Find(recovery).All(candidate => candidate.DocumentId != id),
            "Closing a project on purpose leaves no recovery copy behind");

        // Declining the offer deletes the copy, so it is not asked about again.
        Invoke("EmergencySave");
        var third = new MainWindow { Width = 800, Height = 600 };
        try
        {
            third.Show();
            SetInstanceField(third, "_recoveryPromptSink", (Func<string, bool>)(_ => false));
            Instance(third, "OfferRecovery");
        }
        finally
        {
            third.Close();
        }
        Check(AutoRecovery.Find(recovery).All(candidate => candidate.DocumentId != id), "Declining the offer deletes the copy");

        // An error is written to the log, with its place and the exception.
        var logs = Environment.GetEnvironmentVariable("G3D_LOG_DIR")!;
        var reporter = typeof(MainWindow).Assembly.GetType("Guanacaste3D.Desktop.CrashReporter")!;
        reporter.GetMethod("Log", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, ["interface", new InvalidOperationException("prueba de registro")]);
        Check(Directory.Exists(logs) && Directory.EnumerateFiles(logs, "errors-*.log")
                .Any(file => File.ReadAllText(file).Contains("prueba de registro")),
            "An error is written to the day's log with its place and details");

        // Leave the house as the next checks expect: pinned state restored.
        session.SetWallsPinned([wall.Id], false);
    }

    private static object? Instance(MainWindow window, string name) =>
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(window, null);

    private static T InstanceField<T>(MainWindow window, string name) =>
        (T)typeof(MainWindow).GetField(name, PrivateInstance)!.GetValue(window)!;

    private static void SetInstanceField(MainWindow window, string name, object? value) =>
        typeof(MainWindow).GetField(name, PrivateInstance)!.SetValue(window, value);

    /// <summary>
    /// Opt-in diagnostic (G3D_PROFILE=1): times each stage of the redraw that follows every edit, on the real house,
    /// because the model itself changes in about 0.1 s and the rest is the interface catching up.
    /// </summary>
    private static void ProfileRefresh()
    {
        foreach (var stage in new[]
                 {
                     "SynchronizeModelViewGraphics", "UpdateWindowTitle", "RefreshLevelPicker", "RefreshViewShortcuts",
                     "RefreshTree", "RefreshProperties", "RenderPlan", "Render3D", "RefreshWallTypePicker", "RefreshSchedule",
                     "RenderSheet", "RenderOrthographicViews", "RefreshExtendedWorkflows"
                 })
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Invoke(stage);
            Pump();
            Console.WriteLine($"PROFILE: {stage,-32} {timer.ElapsedMilliseconds,6} ms");
        }
    }

    /// <summary>
    /// Random ribbon commands on random real walls of the house, on both levels, one seed after another. Every command
    /// redraws a scene of about 400,000 triangles, so the run has a time budget instead of a fixed length.
    /// </summary>
    private static void RandomRibbonOnHouse(DocumentSession session)
    {
        string[] commands =
        [
            "AllowJoin", "MiterJoin", "ButtJoin", "SquareOff", "DisallowJoin", "Pin", "FlipWall", "DetachAll",
            "Copy", "Cut", "Paste", "CopyElement", "Rotate", "Undo", "Redo"
        ];
        // Rotating a pinned wall shows a warning box that waits for the user; the window offers a sink for checks.
        var warnings = new List<string>();
        SetField("_rotationWarningSink", (Action<string, string>)((message, _) => warnings.Add(message)));
        var budget = System.Diagnostics.Stopwatch.StartNew();
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("G3D_RANDOM_SECONDS"), out var requested) && requested > 0 ? requested : 100;
        var failures = new List<string>();
        var executed = 0;
        // DocumentSession intentionally retains only the 40 most recent undo snapshots.
        // Keep this fixture's random edits below that limit (including earlier ribbon edits),
        // so the final full-undo assertion remains valid instead of depending on runner speed.
        const int maxRandomCommands = 20;
        for (var seed = 1; seed <= 12 && executed < maxRandomCommands && budget.Elapsed < TimeSpan.FromSeconds(seconds); seed++)
        {
            var random = new Random(seed * 131);
            for (var step = 0; step < 30 && executed < maxRandomCommands && budget.Elapsed < TimeSpan.FromSeconds(seconds); step++)
            {
                var wall = session.Document.Walls[random.Next(session.Document.Walls.Count)];
                var command = commands[random.Next(commands.Length)];
                // Reach the wall's level the way the user does; now and then clear the selection altogether.
                Invoke("SelectElement", wall.Id);
                if (random.Next(10) == 0) SetField("_selectedWallId", null);
                try
                {
                    Invoke("ExecuteApplicationCommand", command);
                    session.Document.Validate();
                    executed++;
                }
                catch (Exception exception)
                {
                    var cause = (exception as TargetInvocationException)?.InnerException ?? exception;
                    failures.Add($"seed {seed} step {step} {command}: {cause.GetType().Name}: {cause.Message}");
                    break;
                }
            }
        }
        Check(failures.Count == 0 && executed >= Math.Min(15, seconds / 4),
            $"{executed} random ribbon commands on the house's real walls, on both levels, never throw and keep the project valid" +
            (failures.Count == 0 ? "" : ": " + string.Join(" | ", failures.Take(5))));
    }
}
