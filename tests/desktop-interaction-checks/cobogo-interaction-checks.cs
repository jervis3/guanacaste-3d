using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            // A curved wall is drawn with three clicks on the plan, the way a person does, and is one wall afterwards.
            Invoke("SelectElement", session.Document.Walls[0].Id);
            tabs.SelectedIndex = 0;
            Pump();
            Invoke("SetWallTool", true);
            ((System.Windows.Controls.RadioButton)opened.FindName("WallArcMode")).IsChecked = true;
            Point OnCanvas(double x, double y) => (Point)Invoke("WorldToCanvas", new Point2(x, y))!;
            var wallsBeforeArc = session.Document.Walls.Count;
            Invoke("HandleWallPlacementClick", OnCanvas(540, 540));
            Invoke("HandleWallPlacementClick", OnCanvas(548, 548));
            Check(Field<Point2?>("_pendingArcEnd") is not null && session.Document.Walls.Count == wallsBeforeArc,
                "After the second click an arc is waiting for its third point and nothing has been built yet");
            Invoke("HandleWallPlacementClick", OnCanvas(541.5, 547));
            var arcPieces = session.Document.Walls.Skip(wallsBeforeArc).ToArray();
            Check(arcPieces.Length >= 5 && arcPieces.Select(piece => piece.CurveGroupId).Distinct().Count() == 1 && arcPieces[0].CurveGroupId is not null &&
                  Field<Point2?>("_pendingArcEnd") is null && Field<Point2?>("_pendingWallStart") is null,
                $"The third click builds one curved wall of {arcPieces.Length} pieces and ends the sketch");
            Invoke("SetWallTool", false);
            Invoke("RenderPlan");
            Pump();
            Check(arcPieces.All(piece => session.Document.Walls.Any(wall => wall.Id == piece.Id)), "The plan redraws with the curved wall in it");

            // The curved wall is dimensioned from the Annotate tab: one click per dimension, and a straight wall is refused.
            var dimensionsBefore = session.Document.Dimensions.Count;
            foreach (var command in new[] { "RadialDimension", "DiameterDimension", "ArcLengthDimension" })
            {
                Invoke("ExecuteApplicationCommand", command);
                Check(Field<Func<Guid, bool>?>("_pickWallAction") is not null, $"{command} waits for a click on a wall");
                Field<Func<Guid, bool>>("_pickWallAction")(arcPieces[arcPieces.Length / 2].Id);
            }
            var added = session.Document.Dimensions.Skip(dimensionsBefore).ToArray();
            Check(added.Length == 3 && added.Select(dimension => dimension.Kind).Distinct().Count() == 3 &&
                  added.All(dimension => PlanDimensions.Resolve(session.Document, dimension) is { Value: > 0 }),
                "The radius, the diameter and the arc length of the curved wall are added and read their values");
            // The angle between two walls takes two clicks, and the first wall alone changes nothing.
            var angleLevel = session.Document.Levels[0].Id;
            var angleA = session.AddWall(angleLevel, new Point2(560, 560), new Point2(564, 560));
            var angleB = session.AddWall(angleLevel, new Point2(560, 560), new Point2(560, 564));
            var dimensionsBeforeAngle = session.Document.Dimensions.Count;
            Invoke("ExecuteApplicationCommand", "AngularDimension");
            Check(Field<Func<Guid, bool>>("_pickWallAction")(angleA) == false && session.Document.Dimensions.Count == dimensionsBeforeAngle,
                "The first click of an angular dimension only asks for the second wall");
            Field<Func<Guid, bool>>("_pickWallAction")(angleB);
            Check(session.Document.Dimensions.Count == dimensionsBeforeAngle + 1 &&
                  PlanDimensions.Resolve(session.Document, session.Document.Dimensions[^1]) is { IsAngle: true, Value: > 89.9 and < 90.1 },
                "The second click adds a 90° angular dimension between the two walls");
            Invoke("RenderPlan");
            Pump();

            // The Detail group: each button arms its own tool, and what it draws is shown in the plan and never in 3D.
            foreach (var (command, kind, mode) in new[]
                     {
                         ("DetailLine", StructuralElementKind.DetailLine, "Line"), ("FilledRegion", StructuralElementKind.FilledRegion, "Rectangle"),
                         ("RevisionCloud", StructuralElementKind.RevisionCloud, "Rectangle"), ("Insulation", StructuralElementKind.Insulation, "Line")
                     })
            {
                Invoke("ExecuteApplicationCommand", command);
                Check(Field<object>("_structuralTool").ToString() == $"({kind}, {mode})", $"{command} arms a {mode.ToLowerInvariant()} tool for its own kind");
                var elementsBefore = session.Document.StructuralElements.Count;
                Invoke("CreateStructuralFromTool", kind, new Point2(580, 580), new Point2(586, 584));
                Invoke("RenderPlan");
                Pump();
                var drawn = session.Document.StructuralElements[^1];
                Check(session.Document.StructuralElements.Count == elementsBefore + 1 && drawn.Kind == kind && !drawn.HasSolid &&
                      ((Canvas)opened.FindName("PlanCanvas")).Children.OfType<FrameworkElement>().Any(shape => shape.Tag is Guid tag && tag == drawn.Id),
                    $"{command} adds its element and the plan draws it");
            }
            Invoke("CancelStructuralTool");

            // Project units: the window changes how the wall panel and the dimensions read, and typing a unit is understood.
            T InWindow<T>(string field, string name) where T : FrameworkElement =>
                (T)System.Windows.LogicalTreeHelper.FindLogicalNode(Field<Window>(field), name)!;
            void ClickIn(string field, string name) =>
                InWindow<System.Windows.Controls.Button>(field, name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Invoke("SelectElement", angleA);
            Invoke("RefreshProperties");
            Pump();
            var lengthBox = (System.Windows.Controls.TextBox)opened.FindName("PropertyLengthInput");
            Check(lengthBox.Text.StartsWith("4", StringComparison.Ordinal) && lengthBox.Text.Contains('.'), $"The wall panel shows the 4 m wall as {lengthBox.Text} in meters");
            Invoke("ExecuteApplicationCommand", "ProjectUnits");
            Pump();
            InWindow<System.Windows.Controls.ComboBox>("_unitsWindow", "UnitLengthBox").SelectedIndex = (int)LengthUnit.Feet;
            InWindow<System.Windows.Controls.ComboBox>("_unitsWindow", "UnitDecimalsBox").SelectedIndex = 1;
            Check(InWindow<System.Windows.Controls.TextBlock>("_unitsWindow", "UnitSample").Text.Contains("ft"), "The units window shows a sample in the unit chosen");
            ClickIn("_unitsWindow", "UnitApply");
            Invoke("SelectElement", angleA);
            Invoke("RefreshProperties");
            Pump();
            Check(session.Document.Units.Length == LengthUnit.Feet && lengthBox.Text == "13.1",
                $"After choosing feet with one decimal the wall panel shows the 4 m wall as {lengthBox.Text}");
            var dimensionOfWall = session.Document.Dimensions.Last();
            Invoke("ApplyDimensionText", dimensionOfWall, "30 cm");
            Check(session.Document.Dimensions.Count > 0, "A dimension edit typed with a unit (30 cm) is understood while the project is in feet");
            ClickIn("_unitsWindow", "UnitClose");
            session.SetProjectUnits(ProjectUnits.Default);

            // Site: the Position window turns true north and the sun of the 3D view follows it; Coordinates converts points.
            Invoke("ExecuteApplicationCommand", "Position");
            Pump();
            var sunStraight = (System.Numerics.Vector3)Invoke("ModelSunDirection")!;
            InWindow<System.Windows.Controls.TextBox>("_positionWindow", "SiteTrueNorth").Text = "90";
            ClickIn("_positionWindow", "SitePositionApply");
            var sunTurned = (System.Numerics.Vector3)Invoke("ModelSunDirection")!;
            Check(Math.Abs(session.Document.Site.TrueNorthDegrees - 90) < 1e-9 && Math.Abs(sunTurned.Z - sunStraight.Z) < 1e-5 &&
                  Math.Abs(sunTurned.X - -sunStraight.Y) < 1e-5 && Math.Abs(sunTurned.Y - sunStraight.X) < 1e-5,
                "Setting true north to 90° turns the sun of the 3D view with the project and keeps its height");
            InWindow<System.Windows.Controls.TextBox>("_positionWindow", "SiteTrueNorth").Text = "400";
            ClickIn("_positionWindow", "SitePositionApply");
            Check(Math.Abs(session.Document.Site.TrueNorthDegrees - 90) < 1e-9, "An angle out of range changes nothing");
            ClickIn("_positionWindow", "SitePositionClose");
            Invoke("RenderPlan");
            Pump();
            Invoke("ExecuteApplicationCommand", "Coordinates");
            Pump();
            InWindow<System.Windows.Controls.TextBox>("_coordinatesWindow", "SiteEast").Text = "500000";
            InWindow<System.Windows.Controls.TextBox>("_coordinatesWindow", "SiteNorth").Text = "1170000";
            ClickIn("_coordinatesWindow", "SiteApply");
            Check(session.Document.Site.SharedEast == 500000 && session.Document.Site.SharedNorth == 1170000, "The Coordinates window stores the survey coordinates of the project origin");
            InWindow<System.Windows.Controls.TextBox>("_coordinatesWindow", "SiteProjectPoint").Text = "0 1";
            ClickIn("_coordinatesWindow", "SiteToShared");
            Check(InWindow<System.Windows.Controls.TextBlock>("_coordinatesWindow", "SiteResult").Text.Contains("500001"),
                "Project north one meter from the origin reads as one meter east of it, because true north is at 90°");
            ClickIn("_coordinatesWindow", "SiteClose");
            session.SetProjectSite(ProjectSite.Default);

            // Shared parameters: a file with parameters is opened in the window and its parameters are added to the project.
            var sharedFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"shared-{Guid.NewGuid():N}.txt");
            System.IO.File.WriteAllText(sharedFile, SharedParameterFile.Write(
                [new ParameterDefinition(Guid.NewGuid(), "Código de obra", ParameterSpec.Text, ParameterBinding.Instance, [ElementCategory.Walls], "Identidad"),
                 new ParameterDefinition(Guid.NewGuid(), "Carga térmica", ParameterSpec.Number, ParameterBinding.Type, [ElementCategory.Walls], "Energía")]));
            var parametersBefore = session.Document.ParameterDefinitions.Count;
            Invoke("ExecuteApplicationCommand", "SharedParameters");
            Pump();
            Check((bool)Invoke("LoadSharedParameterFile", sharedFile)! && InWindow<System.Windows.Controls.ListBox>("_sharedWindow", "SharedList").Items.Count == 2,
                "The Shared Parameters window lists the two parameters of the file");
            ClickIn("_sharedWindow", "SharedAdd");
            Check(session.Document.ParameterDefinitions.Count == parametersBefore + 2, "Adding to the project brings both parameters in");
            ClickIn("_sharedWindow", "SharedAdd");
            Check(session.Document.ParameterDefinitions.Count == parametersBefore + 2, "Adding them again adds nothing");
            var exportFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"export-{Guid.NewGuid():N}.txt");
            Check((bool)Invoke("ExportSharedParameters", exportFile)! && SharedParameterFile.Read(System.IO.File.ReadAllText(exportFile)).Parameters.Count == parametersBefore + 2,
                "The project's own parameters are exported as a shared parameter file");
            System.IO.File.Delete(sharedFile);
            System.IO.File.Delete(exportFile);
            ClickIn("_sharedWindow", "SharedClose");
            Check(!(bool)Invoke("LoadSharedParameterFile", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-such-shared-file.txt"))!,
                "A file that does not exist is reported, not opened");

            // Wall profile: the editor opens on the selected wall, presets fill it, Apply shapes the wall and Reset flattens it.
            T InProfile<T>(string name) where T : FrameworkElement =>
                (T)System.Windows.LogicalTreeHelper.FindLogicalNode(Field<Window>("_profileWindow"), name)!;
            void ClickProfile(string name) =>
                InProfile<System.Windows.Controls.Button>(name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Invoke("SelectElement", angleA);
            Invoke("ExecuteApplicationCommand", "EditProfile");
            Pump();
            Check(Field<Window?>("_profileWindow")?.IsVisible == true && InProfile<System.Windows.Controls.TextBox>("ProfileText").Text.Contains(';'),
                "Edit Profile opens the editor on the selected wall with its top as a list of points");
            InProfile<System.Windows.Controls.TextBox>("ProfileText").Text = "esto no es un perfil";
            ClickProfile("ProfileApply");
            Check(session.Document.Walls.Single(wall => wall.Id == angleA).TopProfile is null &&
                  InProfile<System.Windows.Controls.TextBlock>("ProfileMessage").Text.Length > 0,
                "Text that is not a profile changes nothing and the editor says so");
            ClickProfile("ProfileGable");
            ClickProfile("ProfileApply");
            var profiled = session.Document.Walls.Single(wall => wall.Id == angleA);
            Check(profiled.TopProfile is { Count: 3 } && profiled.TopHeightAt(profiled.Length / 2) > profiled.TopHeightAt(0),
                "The gable preset applied from the editor gives the wall a ridge higher than its ends");
            Invoke("RenderPlan");
            Pump();
            ClickProfile("ProfileClose");
            Invoke("SelectElement", angleA);
            Invoke("ExecuteApplicationCommand", "ResetProfile");
            Check(session.Document.Walls.Single(wall => wall.Id == angleA).TopProfile is null, "Reset Profile flattens the top of the selected wall again");

            Invoke("ExecuteApplicationCommand", "RadialDimension");
            Field<Func<Guid, bool>>("_pickWallAction")(session.Document.Walls[0].Id);
            Check(session.Document.Dimensions.Count == dimensionsBefore + 4, "A straight wall gets no radius and the status line says why");
            SetField("_pickWallAction", null);
            Invoke("RenderPlan");
            Pump();

            // The level section box shows one level in 3D, and pressing it again brings everything back.
            tabs.SelectedIndex = 1;
            Pump();
            var everything = ((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count;
            Invoke("SelectElement", session.Document.Walls[0].Id);
            var activeLevel = Field<Guid>("_activeLevelId");
            Invoke("ExecuteApplicationCommand", "SectionBox");
            var shown = ((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls;
            Check(shown.Count > 0 && shown.Count < everything && shown.All(wall => wall.LevelId == activeLevel),
                $"The section box leaves {shown.Count} of the house's {everything} walls, all of the active level");
            Invoke("ExecuteApplicationCommand", "SectionBox");
            Check(((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count == everything,
                "Pressing the section box again shows the whole house");
            tabs.SelectedIndex = 0;
            Pump();

            // Elevation and section are drawn when their tabs open, not on every edit, and they show the current model.
            var elevationCanvas = (Canvas)opened.FindName("ElevationCanvas");
            tabs.SelectedIndex = 4;
            Pump();
            Check(elevationCanvas.Children.Count > 20, $"The elevation tab draws the house when it opens ({elevationCanvas.Children.Count} shapes)");
            var polygonsFlat = elevationCanvas.Children.OfType<System.Windows.Shapes.Polygon>().Count();
            tabs.SelectedIndex = 0;
            Pump();
            session.SetWallProfile(angleA, WallProfiles.Gable(4, 3));
            tabs.SelectedIndex = 4;
            Pump();
            Check(elevationCanvas.Children.OfType<System.Windows.Shapes.Polygon>().Count() > polygonsFlat,
                "A wall with a profile is drawn in the elevation as the polygon under its top, not as a rectangle");
            session.ResetWallProfiles([angleA]);
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

            // The same operations from the keyboard: Ctrl+C, Ctrl+V and Ctrl+X, and nothing without Control.
            Invoke("SelectElement", longest.Id);
            Check(!(bool)Invoke("TryHandleClipboardShortcut", Key.C, ModifierKeys.None)! &&
                  !(bool)Invoke("TryHandleClipboardShortcut", Key.C, ModifierKeys.Control | ModifierKeys.Shift)!,
                "The letter C alone, or with Shift as well, is not a copy shortcut");
            Check((bool)Invoke("TryHandleClipboardShortcut", Key.C, ModifierKeys.Control)! &&
                  (bool)Invoke("TryHandleClipboardShortcut", Key.V, ModifierKeys.Control)! &&
                  session.Document.Walls.Count == wallCount + 1,
                "Ctrl+C then Ctrl+V pastes a copy of the selected wall");
            Check((bool)Invoke("TryHandleClipboardShortcut", Key.X, ModifierKeys.Control)! && session.Document.Walls.Count == wallCount,
                "Ctrl+X cuts the selected wall");
            Invoke("TryHandleClipboardShortcut", Key.V, ModifierKeys.Control);
            Invoke("TryHandleClipboardShortcut", Key.X, ModifierKeys.Control);
            Check(session.Document.Walls.Count == wallCount, "Ctrl+V after Ctrl+X puts the wall back and a further Ctrl+X takes it away again");

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

                // View filters: a typed rule hides the walls it matches in the active view only.
                tabs.SelectedIndex = 1;
                Pump();
                var activeView = (ProjectView)Invoke("ActiveProjectView")!;
                var wallsInView = ((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count;
                Answer("espesor > 0.01");
                Invoke("ExecuteApplicationCommand", "Filters");
                var filteredView = session.Document.Views.First(view => view.Id == activeView.Id);
                Check(filteredView.Filters.Count == 1 && ((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count == 0 &&
                      session.Document.Walls.Count > 0,
                    $"The Filters command hides all {wallsInView} walls of the active view with a rule on thickness, and the house keeps them");
                Answer("blah blah blah");
                Invoke("ExecuteApplicationCommand", "Filters");
                Check(session.Document.Views.First(view => view.Id == activeView.Id).Filters.Count == 1, "Text that is not a rule changes nothing");
                Answer("quitar");
                Invoke("ExecuteApplicationCommand", "Filters");
                Check(session.Document.Views.First(view => view.Id == activeView.Id).Filters.Count == 0 &&
                      ((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count == wallsInView,
                    "Typing «quitar» removes the filters and the view shows its walls again");
                // Phases: the window opens with the two default phases and works on the selected wall and the active view.
                T Named<T>(string windowField, string name) where T : FrameworkElement =>
                    (T)System.Windows.LogicalTreeHelper.FindLogicalNode(Field<Window>(windowField), name)!;
                void Click(string windowField, string name) =>
                    Named<System.Windows.Controls.Button>(windowField, name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                tabs.SelectedIndex = 1;
                Pump();
                var totalWalls = ((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count;
                Invoke("SelectElement", longest.Id);
                Invoke("ExecuteApplicationCommand", "Phases");
                Pump();
                var phaseIds = session.Document.Phases.Select(phase => phase.Id).ToArray();
                Check(phaseIds.Length == 2 && Field<Window>("_phasesWindow").IsVisible, "The Phases command opens its window with an existing and a new phase");
                Named<System.Windows.Controls.ComboBox>("_phasesWindow", "PhaseCreatedBox").SelectedIndex = 2;
                Click("_phasesWindow", "PhaseAssign");
                Check(session.Document.PhaseAssignments.Any(item => item.ElementId == longest.Id && item.CreatedPhaseId == phaseIds[1]),
                    "Assigning from the window creates the selected wall in the new phase");
                Named<System.Windows.Controls.ComboBox>("_phasesWindow", "PhaseViewBox").SelectedIndex = 2;
                Named<System.Windows.Controls.ComboBox>("_phasesWindow", "PhaseFilterBox").SelectedIndex = (int)PhaseFilter.ShowExisting;
                Click("_phasesWindow", "PhaseViewApply");
                Check(((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count == totalWalls - 1,
                    "Showing only the existing in the new phase hides the one wall that is new");
                Named<System.Windows.Controls.ComboBox>("_phasesWindow", "PhaseFilterBox").SelectedIndex = (int)PhaseFilter.ShowAll;
                Click("_phasesWindow", "PhaseViewApply");
                Answer("Demolición");
                Click("_phasesWindow", "PhaseAdd");
                Check(session.Document.Phases.Count == 3 && session.Document.Phases[2].Name == "Demolición", "A phase added from the window joins the end of the list");
                Click("_phasesWindow", "PhaseClose");
                Pump();
                Check(!Field<Window?>("_phasesWindow")?.IsVisible ?? true, "The window closes");

                // Design options: a set, an alternative, and the wall moved into it.
                Invoke("ExecuteApplicationCommand", "DesignOptions");
                Pump();
                Answer("Fachada", "Cobogó");
                Click("_optionsWindow", "OptionSetAdd");
                Answer("Muro liso");
                Click("_optionsWindow", "OptionAdd");
                Check(session.Document.OptionSets.Count == 1 && session.Document.OptionSets[0].Options.Select(option => option.Name).SequenceEqual(["Cobogó", "Muro liso"]),
                    "The window makes the Fachada set with its primary option and an alternative");
                Named<System.Windows.Controls.ListBox>("_optionsWindow", "OptionList").SelectedIndex = 1;
                Click("_optionsWindow", "OptionAssign");
                Check(((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count == totalWalls - 1,
                    "A wall put in the alternative disappears from a view that shows the primary option");
                Named<System.Windows.Controls.ComboBox>("_optionsWindow", "OptionViewBox").SelectedIndex = 1;
                Click("_optionsWindow", "OptionViewApply");
                Check(((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count == totalWalls,
                    "Choosing the alternative for the view brings it back");
                Click("_optionsWindow", "OptionUnassign");
                Click("_optionsWindow", "OptionClose");
                Pump();

                // Groups: Create Group gathers the selection, and the manager moves, copies, saves, loads and dissolves it.
                tabs.SelectedIndex = 0;
                Pump();
                var groupWalls = new[] { angleA, angleB };
                SetField("_viewSelection", new HashSet<Guid>(groupWalls));
                var wallsWithGroup = session.Document.Walls.Count;
                Answer("Esquina de prueba");
                Invoke("ExecuteApplicationCommand", "CreateGroup");
                Pump();
                var created = session.Document.ElementGroups.SingleOrDefault(item => item.Name == "Esquina de prueba");
                Check(created is { Kind: ElementGroupKind.Group } && created.MemberIds.OrderBy(id => id).SequenceEqual(groupWalls.OrderBy(id => id)) &&
                      Field<Window?>("_groupsWindow")?.IsVisible == true, "Create Group gathers the selected walls and opens the groups window");
                var startBefore = session.Document.Walls.Single(wall => wall.Id == angleA).Start;
                Answer("1 0");
                Click("_groupsWindow", "GroupMove");
                Check(Math.Abs(session.Document.Walls.Single(wall => wall.Id == angleA).Start.X - (startBefore.X + 1)) < 1e-9,
                    "Move in the groups window shifts the group by the distance typed");
                Answer("0 10");
                Click("_groupsWindow", "GroupCopy");
                Check(session.Document.ElementGroups.Count(item => item.Kind == ElementGroupKind.Group) == 2 && session.Document.Walls.Count == wallsWithGroup + 2,
                    "Copy makes a second group with its own walls");
                var saveFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"group-{Guid.NewGuid():N}.g3dgroup");
                Check((bool)Invoke("SaveGroupFile", created!.Id, saveFile)! && System.IO.File.ReadAllText(saveFile).Contains("G3D-GROUP/1"), "A group is saved to a file");
                Answer("0 20");
                Check((bool)Invoke("LoadGroupFile", saveFile)! && session.Document.Walls.Count == wallsWithGroup + 4,
                    "The saved file loads back as a third group, shifted 20 m");
                System.IO.File.Delete(saveFile);
                Answer("no es una pareja");
                Check(!(bool)Invoke("LoadGroupFile", System.IO.Path.Combine(System.IO.Path.GetTempPath(), "missing.g3dgroup"))!, "A bad shift or a missing file loads nothing");
                Click("_groupsWindow", "GroupUngroup");
                Check(session.Document.ElementGroups.Count(item => item.Kind == ElementGroupKind.Group) == 2 && session.Document.Walls.Count == wallsWithGroup + 4,
                    "Dissolving a group leaves its walls where they are");
                Click("_groupsWindow", "GroupClose");
                // Masses and site: the buttons arm their tools, and walls and a roof are built from a selected mass.
                Invoke("ExecuteApplicationCommand", "PlaceMass");
                Check(Field<object>("_structuralTool").ToString() == "(Mass, Rectangle)", "Place Mass arms the mass tool");
                Answer("12");
                Invoke("ExecuteApplicationCommand", "InPlaceMass");
                Check(Field<object>("_structuralTool").ToString() == "(Mass, Rectangle)", "In-Place Mass asks for a height and arms the same tool");
                var massesBefore = session.Document.StructuralElements.Count(element => element.Kind == StructuralElementKind.Mass);
                Invoke("CreateStructuralFromTool", StructuralElementKind.Mass, new Point2(600, 600), new Point2(608, 606));
                var mass = session.Document.StructuralElements.Last();
                Check(mass.Kind == StructuralElementKind.Mass && session.Document.StructuralElements.Count(element => element.Kind == StructuralElementKind.Mass) == massesBefore + 1,
                    "Two corners make a mass");
                Invoke("CancelStructuralTool");
                Invoke("ExecuteApplicationCommand", "ParkingComponent");
                Check(Field<object>("_structuralTool").ToString() == "(ParkingSpace, Rectangle)", "The parking button arms the parking space tool");
                Invoke("CancelStructuralTool");

                var wallsBeforeMass = session.Document.Walls.Count;
                SetField("_selectedStructuralId", (Guid?)mass.Id);
                Invoke("ExecuteApplicationCommand", "WallByFace");
                Check(session.Document.Walls.Count > wallsBeforeMass && (session.Document.Walls.Count - wallsBeforeMass) % 4 == 0,
                    $"Walls by face builds {session.Document.Walls.Count - wallsBeforeMass} walls on the selected mass");
                var floorsBeforeMass = session.Document.Floors.Count;
                SetField("_selectedStructuralId", (Guid?)mass.Id);
                Invoke("ExecuteApplicationCommand", "RoofByFace");
                Check(session.Document.Floors.Count == floorsBeforeMass + 1 && session.Document.Floors.Last().Kind == FloorKind.Roof, "Roof by face puts a roof over the mass");
                SetField("_selectedStructuralId", null);
                Invoke("ExecuteApplicationCommand", "RoofByFace");
                Check(session.Document.Floors.Count == floorsBeforeMass + 1, "Without a selected mass nothing is built");

                tabs.SelectedIndex = 0;
                Pump();
                Invoke("ExecuteApplicationCommand", "ShowMass");
                var planOfView = (ProjectView)Invoke("ActiveProjectView")!;
                Check(planOfView.HiddenCategories.Contains(ViewCategory.Masses), "Show Mass hides the masses in the active view");
                Invoke("ExecuteApplicationCommand", "ShowMass");
                Check(!((ProjectView)Invoke("ActiveProjectView")!).HiddenCategories.Contains(ViewCategory.Masses), "Pressing it again shows them");

                Invoke("ExecuteApplicationCommand", "ContextualData");
                Pump();
                Check(InWindow<System.Windows.Controls.TextBlock>("_contextWindow", "ContextText").Text.Contains("Solsticio de junio"), "Site Data lists the sun's day for the key dates of the year");
                ClickIn("_contextWindow", "ContextClose");

                // Sheet aids: the matchline tool, a guide grid on the open sheet and a reference to another view.
                Invoke("ExecuteApplicationCommand", "Matchline");
                Check(Field<object>("_structuralTool").ToString() == "(Matchline, Line)", "Matchline arms a line tool of its own kind");
                Invoke("CancelStructuralTool");
                tabs.SelectedIndex = 3;
                Pump();
                Answer("30");
                Invoke("ExecuteApplicationCommand", "GuideGrid");
                Check(session.Document.Sheets.Any(sheet => sheet.GuideGridMm == 30), "Guide Grid sets the spacing asked for on the open sheet");
                Answer("abc");
                Invoke("ExecuteApplicationCommand", "GuideGrid");
                Check(session.Document.Sheets.Any(sheet => sheet.GuideGridMm == 30), "A spacing that is not a number changes nothing");
                Answer("0");
                Invoke("ExecuteApplicationCommand", "GuideGrid");
                Check(session.Document.Sheets.All(sheet => sheet.GuideGridMm == 0), "Zero removes the guide grid");
                tabs.SelectedIndex = 0;
                Pump();
                var targetView = session.Document.Views.First(view => view.Kind == ProjectViewKind.Elevation || view.Kind == ProjectViewKind.Section).Name;
                Answer("zzz no existe");
                Invoke("ExecuteApplicationCommand", "ViewReference");
                Check(Field<object>("_structuralTool") is null || Field<object>("_structuralTool").ToString() != "(ViewReference, Point)", "A view name that matches nothing arms nothing");
                Answer(targetView);
                Invoke("ExecuteApplicationCommand", "ViewReference");
                Check(Field<object>("_structuralTool").ToString() == "(ViewReference, Point)", "View Reference asks for a view and arms a click in the plan");
                var referencesBefore = session.Document.StructuralElements.Count(element => element.Kind == StructuralElementKind.ViewReference);
                Invoke("PlaceViewReference", new Point2(603, 603));
                Invoke("RenderPlan");
                Pump();
                Check(session.Document.StructuralElements.Count(element => element.Kind == StructuralElementKind.ViewReference) == referencesBefore + 1 &&
                      session.Document.StructuralElements.Last().Name.StartsWith(targetView, StringComparison.Ordinal), "The click puts the reference, named after the view it points to");
                Invoke("CancelStructuralTool");

                SetField("_viewSelection", null);
                Pump();
                tabs.SelectedIndex = 0;
                Pump();
            }
            finally
            {
                responder.SetValue(null, null);
            }

            CrashProtectionChecks(opened, session);

            RandomRibbonOnHouse(session);

            // Undoing everything the window did returns the house to what was opened, whatever the commands were,
            // as long as the bounded history (40 steps) never dropped the oldest ones.
            var historyTrimmed = session.UndoNames.Count >= 40;
            var guard = 0;
            while (session.CanUndo && guard++ < 10000) session.Undo();
            Check(historyTrimmed || ProjectFile.SerializeDocument(session.Document) == initial,
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
