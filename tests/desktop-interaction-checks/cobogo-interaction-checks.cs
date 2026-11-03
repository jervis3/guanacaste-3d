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

            RandomRibbonOnHouse(session);

            // Undoing everything the window did returns the house to what was opened, whatever the commands were.
            var guard = 0;
            while (session.CanUndo && guard++ < 10000) session.Undo();
            Check(ProjectFile.SerializeDocument(session.Document) == initial,
                "Undoing every command of the session brings the cobogó house back exactly as it opened");
        }
        finally
        {
            opened.Close();
            _window = previous;
            _canvas = (Canvas)previous.FindName("PlanCanvas");
        }
    }

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
        var failures = new List<string>();
        var executed = 0;
        for (var seed = 1; seed <= 12 && budget.Elapsed < TimeSpan.FromSeconds(100); seed++)
        {
            var random = new Random(seed * 131);
            for (var step = 0; step < 30 && budget.Elapsed < TimeSpan.FromSeconds(100); step++)
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
        Check(failures.Count == 0 && executed >= 15,
            $"{executed} random ribbon commands on the house's real walls, on both levels, never throw and keep the project valid" +
            (failures.Count == 0 ? "" : ": " + string.Join(" | ", failures.Take(5))));
    }
}
