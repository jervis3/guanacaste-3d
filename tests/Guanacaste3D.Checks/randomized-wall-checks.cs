using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Interop;

/// <summary>
/// Property-based checks for the wall editing commands. Instead of one hand-written scenario per command, it builds
/// random layouts (corners, tees, free walls, angled walls, doors) and applies random sequences of commands, including
/// hostile arguments, then checks the rules that must hold after every step:
/// a refused command changes nothing, an accepted one leaves a valid project that survives saving, undo and redo are
/// exact, and the geometry stays finite. Every failure names its seed and step, so it can be replayed.
/// </summary>
static class RandomizedWallChecks
{
    private const int DefaultSeeds = 24;
    private const int StepsPerSeed = 45;

    /// <summary>How many times each command was accepted and changed the project, so the run cannot pass by being refused.</summary>
    private static readonly Dictionary<string, int> Accepted = [];

    /// <summary>Runs the random sequences and the targeted random properties.</summary>
    public static void Run(Action<bool, string> check)
    {
        var seeds = int.TryParse(Environment.GetEnvironmentVariable("G3D_FUZZ_SEEDS"), out var requested) && requested > 0
            ? requested : DefaultSeeds;

        var farFailures = new List<string>();
        foreach (var distance in new[] { 0.0, 1e3, 1e5, 1e6, 5e6, 1e7 })
        {
            try
            {
                var far = new DocumentSession(BuildingDocument.Create());
                var farLevel = far.Document.Levels[0].Id;
                var wall = far.AddWall(farLevel, new Point2(distance, distance), new Point2(distance + 5, distance));
                far.AddWall(farLevel, new Point2(distance + 5, distance), new Point2(distance + 5, distance + 3));
                far.AddOpening(wall, OpeningKind.Door, offset: 2);
                CheckGeometry(far.Document, mesh: true);
            }
            catch (Exception exception)
            {
                farFailures.Add($"{distance:0.##E+0} m from the origin: {exception.GetType().Name}: {exception.Message}");
            }
        }
        check(farFailures.Count == 0,
            "Walls far from the origin, such as survey coordinates, still mesh" +
            (farFailures.Count == 0 ? "" : ":\n  " + string.Join("\n  ", farFailures)));

        var failures = new Dictionary<string, string>();
        var steps = 0;
        for (var seed = 1; seed <= seeds; seed++) steps += RunSequence(seed, failures);
        check(failures.Count == 0,
            $"{steps} random wall commands over {seeds} seeds keep the project valid, atomic and exactly undoable" +
            (failures.Count == 0 ? "" : ":\n  " + string.Join("\n  ", failures.Values.Take(8))));

        var neverAccepted = Operations.Select(operation => operation.Name).Where(name => Accepted.GetValueOrDefault(name) == 0).ToArray();
        check(neverAccepted.Length == 0,
            $"Every random command is accepted at least once, so none is only ever refused ({Accepted.Values.Sum()} accepted of {steps})" +
            (neverAccepted.Length == 0 ? "" : ": never accepted " + string.Join(", ", neverAccepted)));

        var propertyFailures = new List<string>();
        for (var seed = 1; seed <= seeds * 4; seed++) Properties(seed, propertyFailures);
        check(propertyFailures.Count == 0,
            $"Random walls keep the algebraic properties of the editing commands over {seeds * 4} seeds" +
            (propertyFailures.Count == 0 ? "" : ":\n  " + string.Join("\n  ", propertyFailures.Take(8))));
    }

    // ------------------------------------------------------------------ random sequences

    private static int RunSequence(int seed, Dictionary<string, string> failures)
    {
        var random = new Random(seed);
        var session = new DocumentSession(BuildingDocument.Create());
        var level = session.Document.Levels[0].Id;
        var upper = session.AddLevel();
        for (var i = 0; i < 4; i++)
        {
            try { AddRandomWall(random, session, level); }
            catch (InvalidDataException) { /* a hostile type id is refused; the sequence starts with fewer walls */ }
        }

        var steps = 0;
        for (var step = 0; step < StepsPerSeed; step++)
        {
            var (name, run) = Operations[random.Next(Operations.Length)];
            var before = ProjectFile.SerializeDocument(session.Document);
            var undoable = session.CanUndo;
            steps++;
            string Where() => $"seed {seed} step {step} {name}";
            void Fail(string kind, string detail) => failures.TryAdd(name + kind, $"{Where()}: {kind} {detail}");

            try
            {
                run(random, session, level, upper);
            }
            catch (InvalidDataException)
            {
                if (ProjectFile.SerializeDocument(session.Document) != before) Fail("not atomic:", "a refused command changed the project");
                continue;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Fail("unexpected exception:", $"{exception.GetType().Name}: {exception.Message}");
                break;
            }

            var after = ProjectFile.SerializeDocument(session.Document);
            if (after == before) continue;
            Accepted[name] = Accepted.GetValueOrDefault(name) + 1;
            try
            {
                session.Document.Validate();
                if (ProjectFile.SerializeDocument(ProjectFile.DeserializeDocument(after)) != after)
                    Fail("round trip:", "saving and reopening changed the project");
                CheckGeometry(session.Document, step % 5 == 0);
                if (Accepted.Values.Sum() % 6 == 0) CheckExports(session.Document);
            }
            catch (Exception exception)
            {
                Fail("invalid result:", $"{exception.GetType().Name}: {exception.Message} @ {Frames(exception)}");
                break;
            }

            if (!session.CanUndo) { Fail("undo:", "a change left nothing to undo"); break; }
            session.Undo();
            if (ProjectFile.SerializeDocument(session.Document) != before) { Fail("undo:", "did not restore the project exactly"); break; }
            session.Redo();
            if (ProjectFile.SerializeDocument(session.Document) != after) { Fail("redo:", "did not reapply the change exactly"); break; }
            _ = undoable;
        }
        return steps;
    }

    /// <summary>
    /// Every exporter must cope with whatever the commands produce: angled walls, tees, butt joins, pinned and
    /// flipped walls. Output must be repeatable and must not contain a number the receiving program cannot read.
    /// </summary>
    private static void CheckExports(BuildingDocument document)
    {
        var stamp = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        var texts = new Dictionary<string, Func<string>>
        {
            ["IFC"] = () => IfcExporter.ToStep(document, stamp),
            ["DXF"] = () => DxfExporter.ToAscii(document),
            ["quantities"] = () => QuantitySchedule.ToCsv(document),
            ["wall layers"] = () => WallLayerQuantitySchedule.ToCsv(document),
            ["roofs"] = () => RoofQuantitySchedule.ToCsv(document),
            ["reveals"] = () => WallOpeningRevealSchedule.ToCsv(document)
        };
        foreach (var (name, export) in texts)
        {
            var first = export();
            if (first != export()) throw new InvalidDataException($"the {name} export is not repeatable");
            if (first.Contains("NaN") || first.Contains("Infinity") || first.Contains("∞"))
                throw new InvalidDataException($"the {name} export contains a number that is not finite");
        }
        var ifc = texts["IFC"]();
        var wallEntities = ifc.Split((char)10).Count(line => line.Contains("=IFCWALL", StringComparison.Ordinal));
        if (wallEntities < document.Walls.Count)
            throw new InvalidDataException($"the IFC export has {wallEntities} walls for {document.Walls.Count} in the project");

        var mesh = BuildingMesher.Build(document);
        var (obj, material) = MeshExporter.ToObj(mesh, "materials.mtl");
        if (obj.Contains("NaN") || material.Contains("NaN")) throw new InvalidDataException("the OBJ export contains NaN");
        if (!MeshExporter.ToGlb(mesh, "random").AsSpan().SequenceEqual(MeshExporter.ToGlb(mesh, "random")))
            throw new InvalidDataException("the GLB export is not repeatable");
        if (PdfPlanExporter.Export(document, SheetPaperSize.A3, stamp).Length == 0)
            throw new InvalidDataException("the PDF export is empty");
        _ = ModelWarnings.Analyze(document);
    }

    /// <summary>The first frames of an exception, so a failure points at the code that raised it.</summary>
    private static string Frames(Exception exception) =>
        string.Join(" < ", (exception.StackTrace ?? string.Empty).Split((char)10).Take(5)
            .Select(line => line.Trim().Split(" in ")[0]));

    /// <summary>The geometry of a result must be finite, whatever commands made it.</summary>
    private static void CheckGeometry(BuildingDocument document, bool mesh)
    {
        var joins = WallJoinIndex.Build(document.Walls);
        foreach (var wall in document.Walls)
        {
            var footprint = joins.Footprint(wall);
            var points = new[] { footprint.StartLeft, footprint.EndLeft, footprint.EndRight, footprint.StartRight };
            if (points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
                throw new InvalidDataException("a wall footprint is not finite");
            var area = 0.0;
            // Relative to the first corner: absolute coordinates would cancel to nothing far from the origin.
            for (var i = 1; i < points.Length - 1; i++)
                area += (points[i].X - points[0].X) * (points[i + 1].Y - points[0].Y) -
                        (points[i + 1].X - points[0].X) * (points[i].Y - points[0].Y);
            if (Math.Abs(area) / 2 < 1e-9)
                throw new InvalidDataException(
                    $"a wall footprint has no area: wall {wall.Start}→{wall.End} t={wall.Thickness:0.###} " +
                    $"joins {wall.StartJoin}/{wall.EndJoin} corners {string.Join(" ", points)} " +
                    $"startJoin={joins.JoinAt(wall.Id, WallEnd.Start)?.Other.Id} endJoin={joins.JoinAt(wall.Id, WallEnd.End)?.Other.Id} " +
                    $"tees={joins.TeeHostAt(wall.Id, WallEnd.Start)?.Id}/{joins.TeeHostAt(wall.Id, WallEnd.End)?.Id}");
        }
        if (!mesh) return;
        var scene = BuildingMesher.Build(document);
        if (scene.Positions.Any(position => !float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)))
            throw new InvalidDataException("the mesh has a vertex that is not finite");
        if (scene.Indices.Any(index => index < 0 || index >= scene.Positions.Count))
            throw new InvalidDataException("the mesh has an index outside its vertices");
    }

    private delegate void Operation(Random random, DocumentSession session, Guid level, Guid upper);

    private static readonly (string Name, Operation Run)[] Operations =
    [
        ("AddWall", (r, s, l, _) => AddRandomWall(r, s, l)),
        ("UpdateWallType", (r, s, _, _) => s.UpdateWallType(Type(r, s), "T", WallFunction.Interior, Thickness(r), "M")),
        ("SetWallType", (r, s, _, _) => s.SetWallType(AnyWall(r, s), Type(r, s))),
        ("UpdateWall", (r, s, _, _) => s.UpdateWall(AnyWall(r, s), Thickness(r), Number(r, 0.1, 6))),
        ("MirrorWalls", (r, s, _, _) => s.MirrorWalls(Some(r, s), Point(r), Point(r), r.Next(2) == 0)),
        ("OffsetWalls", (r, s, _, _) => s.OffsetWalls(Some(r, s), Number(r, -3, 3), r.Next(2) == 0)),
        ("ArrayLinear", (r, s, _, _) => s.ArrayWallsLinear(Some(r, s), r.Next(0, 6), Number(r, -4, 4), Number(r, -4, 4))),
        ("ArrayRadial", (r, s, _, _) => s.ArrayWallsRadial(Some(r, s), r.Next(0, 6), Point(r), Number(r, -120, 120))),
        ("SplitWall", (r, s, _, _) => s.SplitWall(AnyWall(r, s), PointOn(r, s))),
        ("TrimToCorner", (r, s, _, _) => s.TrimToCorner(AnyWall(r, s), Point(r), AnyWall(r, s), Point(r))),
        ("AlignWall", (r, s, _, _) => AlignRandomly(r, s)),
        ("FlipWalls", (r, s, _, _) => s.FlipWalls(Some(r, s))),
        ("PinWalls", (r, s, _, _) => s.SetWallsPinned(Some(r, s), r.Next(2) == 0)),
        ("SetWallJoins", (r, s, _, _) => s.SetWallJoins(Some(r, s), (WallJoinSetting)r.Next(0, 5), r.Next(3) switch { 0 => WallEnd.Start, 1 => WallEnd.End, _ => null })),
        ("MoveWall", (r, s, _, _) => s.MoveWall(AnyWall(r, s), Number(r, -3, 3), Number(r, -3, 3))),
        ("RotateWall", (r, s, _, _) => s.RotateWall(AnyWall(r, s), Number(r, -200, 200))),
        ("RemoveWall", (r, s, _, _) => s.RemoveWall(AnyWall(r, s))),
        ("DuplicateWall", (r, s, _, _) => s.DuplicateWall(AnyWall(r, s), Number(r, -3, 3), Number(r, -3, 3))),
        ("AddOpening", (r, s, _, _) => s.AddOpening(AnyWall(r, s), r.Next(2) == 0 ? OpeningKind.Door : OpeningKind.Window, Number(r, -1, 8))),
        ("PasteWalls", (r, s, l, u) => s.PasteWalls(WallClipboard.Capture(s.Document, Some(r, s)), r.Next(2) == 0 ? l : u, Number(r, -3, 3), Number(r, -3, 3))),
        ("MatchProperties", (r, s, _, _) => s.MatchWallProperties(AnyWall(r, s), Some(r, s))),
        ("TopConstraint", (r, s, _, u) => s.SetWallTopConstraint(AnyWall(r, s), r.Next(2) == 0 ? u : null, Number(r, -1, 1))),
        ("HideCategories", (r, s, _, _) => s.SetViewHiddenCategories(s.Document.Views[0].Id, Enumerable.Range(0, r.Next(0, 4)).Select(_ => (ViewCategory)r.Next(0, 17)).ToArray())),
    ];

    // ------------------------------------------------------------------ random inputs

    /// <summary>
    /// Random walls are almost never parallel, so most of the time this first makes a parallel copy of a wall and
    /// aligns the wall to it. Both steps are one undo step, as one command would be.
    /// </summary>
    private static void AlignRandomly(Random random, DocumentSession session)
    {
        var wall = AnyWall(random, session);
        if (random.Next(4) == 0 || session.Document.Walls.All(candidate => candidate.Id != wall))
        {
            session.AlignWall(wall, random.Next(-2, 3), AnyWall(random, session), random.Next(-2, 3));
            return;
        }
        using var group = session.BeginGroup("Align");
        var parallel = session.OffsetWalls([wall], Number(random, -4, 4), copy: true)[0];
        session.AlignWall(wall, random.Next(-1, 2), parallel, random.Next(-1, 2));
        group.Assimilate();
    }

    /// <summary>Mostly sensible numbers, now and then the values that break careless code.</summary>
    private static double Number(Random random, double low, double high) => random.Next(40) switch
    {
        0 => double.NaN,
        1 => double.PositiveInfinity,
        2 => 0,
        3 => 1e9,
        4 => -1e9,
        _ => low + random.NextDouble() * (high - low)
    };

    private static double Thickness(Random random) => random.Next(12) == 0 ? Number(random, 0, 1) : 0.06 + random.NextDouble() * 0.5;

    private static Point2 Point(Random random) => new(Number(random, -15, 15), Number(random, -15, 15));

    private static Guid Type(Random random, DocumentSession session) =>
        random.Next(15) == 0 ? Guid.NewGuid() : session.Document.WallTypes[random.Next(session.Document.WallTypes.Count)].Id;

    private static Guid AnyWall(Random random, DocumentSession session) =>
        session.Document.Walls.Count == 0 || random.Next(25) == 0
            ? Guid.NewGuid()
            : session.Document.Walls[random.Next(session.Document.Walls.Count)].Id;

    private static IReadOnlyCollection<Guid> Some(Random random, DocumentSession session) =>
        Enumerable.Range(0, random.Next(0, 4)).Select(_ => AnyWall(random, session)).ToArray();

    /// <summary>A point on a random wall's centerline, now and then a point off it.</summary>
    private static Point2 PointOn(Random random, DocumentSession session)
    {
        if (session.Document.Walls.Count == 0 || random.Next(10) == 0) return Point(random);
        var wall = session.Document.Walls[random.Next(session.Document.Walls.Count)];
        var t = random.NextDouble();
        return new Point2(wall.Start.X + (wall.End.X - wall.Start.X) * t, wall.Start.Y + (wall.End.Y - wall.Start.Y) * t);
    }

    /// <summary>Adds a free wall, a wall continuing another, or one resting on another's body, at a random angle.</summary>
    private static void AddRandomWall(Random random, DocumentSession session, Guid level)
    {
        var walls = session.Document.Walls;
        var angle = (random.Next(3) == 0 ? random.NextDouble() * 360 : random.Next(24) * 15) * Math.PI / 180;
        var length = random.Next(8) == 0 ? 0.05 + random.NextDouble() * 0.2 : 0.5 + random.NextDouble() * 10;
        var start = walls.Count == 0 || random.Next(3) == 0
            ? new Point2(Math.Round(random.NextDouble() * 30 - 15, 2), Math.Round(random.NextDouble() * 30 - 15, 2))
            : random.Next(2) == 0
                ? walls[random.Next(walls.Count)].End
                : PointOn(random, session);
        var end = new Point2(start.X + Math.Cos(angle) * length, start.Y + Math.Sin(angle) * length);
        session.AddWall(level, start, end, wallTypeId: Type(random, session),
            locationReference: (WallLocationReference)random.Next(0, 3));
    }

    // ------------------------------------------------------------------ algebraic properties

    /// <summary>Properties that hold for every random wall, checked on a document with a single family of walls.</summary>
    private static void Properties(int seed, List<string> failures)
    {
        var random = new Random(seed * 7919);
        void Fail(string text) => failures.Add($"seed {seed}: {text}");
        var session = new DocumentSession(BuildingDocument.Create());
        var level = session.Document.Levels[0].Id;

        // A lone wall keeps the face it was placed by whatever its new thickness, and any thickness comes back.
        var reference = (WallLocationReference)random.Next(0, 3);
        var start = new Point2(random.NextDouble() * 20 - 10, random.NextDouble() * 20 - 10);
        var angle = random.NextDouble() * 2 * Math.PI;
        var length = 2.5 + random.NextDouble() * 7.5;
        var id = session.AddWall(level, start, new Point2(start.X + Math.Cos(angle) * length, start.Y + Math.Sin(angle) * length),
            wallTypeId: BuiltInWallTypes.Basic200Id, locationReference: reference);
        var original = session.Document.Walls.Single();
        var thickness = 0.06 + random.NextDouble() * 0.5;
        session.UpdateWall(id, thickness, original.Height);
        var resized = session.Document.Walls.Single();
        double Face(Wall wall, int side) => FacePlane(wall, side);
        var fixedSide = reference switch { WallLocationReference.ExteriorFace => -1, WallLocationReference.InteriorFace => 1, _ => 0 };
        if (Math.Abs(Face(original, fixedSide) - Face(resized, fixedSide)) > 1e-9)
            Fail($"the {reference} plane moved by {Face(resized, fixedSide) - Face(original, fixedSide):0.#########} m when thickness went {original.Thickness:0.###}→{thickness:0.###}");
        session.UpdateWall(id, original.Thickness, original.Height);
        var restored = session.Document.Walls.Single();
        if (restored.Start.DistanceTo(original.Start) > 1e-9 || restored.End.DistanceTo(original.End) > 1e-9)
            Fail("changing a thickness and changing it back did not return the wall to where it started");

        // Flipping twice and mirroring twice about one axis are the identity.
        var before = ProjectFile.SerializeDocument(session.Document);
        session.FlipWalls([id]);
        session.FlipWalls([id]);
        if (ProjectFile.SerializeDocument(session.Document) != before) Fail("flipping twice changed the project");
        var axisStart = new Point2(random.NextDouble() * 10 - 5, random.NextDouble() * 10 - 5);
        var axisEnd = new Point2(axisStart.X + 1 + random.NextDouble() * 5, axisStart.Y + random.NextDouble() * 5 - 2.5);
        var snapshot = session.Document.Walls.Single();
        session.MirrorWalls([id], axisStart, axisEnd, copy: false);
        session.MirrorWalls([id], axisStart, axisEnd, copy: false);
        var twice = session.Document.Walls.Single();
        if (twice.Start.DistanceTo(snapshot.Start) > 1e-8 || twice.End.DistanceTo(snapshot.End) > 1e-8 ||
            twice.LocationReference != snapshot.LocationReference || twice.StartJoin != snapshot.StartJoin)
            Fail("mirroring twice about the same axis did not restore the wall");

        // Offsetting a copy by d and the original back by -d give two walls with the same geometry.
        var d = (random.NextDouble() - 0.5) * 6;
        if (Math.Abs(d) > 0.01)
        {
            var copy = session.OffsetWalls([id], d, copy: true)[0];
            session.OffsetWalls([copy], -d, copy: false);
            var a = session.Document.Walls.Single(wall => wall.Id == id);
            var b = session.Document.Walls.Single(wall => wall.Id == copy);
            if (a.Start.DistanceTo(b.Start) > 1e-8 || a.End.DistanceTo(b.End) > 1e-8) Fail("offset and offset back did not meet");
        }

        // A split keeps the total length and every opening.
        var host = session.Document.Walls.First(wall => wall.Id == id);
        var door = session.AddOpening(id, OpeningKind.Door, offset: host.Length * 0.25);
        var cut = 0.5 + random.NextDouble() * (host.Length - 1);
        try
        {
            var second = session.SplitWall(id, new Point2(
                host.Start.X + (host.End.X - host.Start.X) * cut / host.Length, host.Start.Y + (host.End.Y - host.Start.Y) * cut / host.Length));
            var parts = session.Document.Walls.Where(wall => wall.Id == id || wall.Id == second).ToArray();
            if (Math.Abs(parts.Sum(wall => wall.Length) - host.Length) > 1e-8) Fail("splitting a wall changed its total length");
            if (session.Document.Openings.All(opening => opening.Id != door)) Fail("splitting a wall lost an opening");
            var doorWorld = host.Start.X + (host.End.X - host.Start.X) * (host.Length * 0.25) / host.Length;
            var hosted = session.Document.Openings.Single(opening => opening.Id == door);
            var holder = parts.Single(wall => wall.Id == hosted.WallId);
            var doorNow = holder.Start.X + (holder.End.X - holder.Start.X) * hosted.Offset / holder.Length;
            if (Math.Abs(doorNow - doorWorld) > 1e-8) Fail("splitting a wall moved its door");
        }
        catch (InvalidDataException)
        {
            // The cut may fall within the door: refusing is the right answer.
        }
    }

    /// <summary>Signed distance of a wall's centerline (side 0) or a face (-1 right, 1 left) from the origin along its left normal.</summary>
    private static double FacePlane(Wall wall, int side)
    {
        var normalX = -(wall.End.Y - wall.Start.Y) / wall.Length;
        var normalY = (wall.End.X - wall.Start.X) / wall.Length;
        return wall.Start.X * normalX + wall.Start.Y * normalY + side * wall.Thickness / 2;
    }
}
