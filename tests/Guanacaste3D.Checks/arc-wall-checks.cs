using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

/// <summary>Checks curved walls: the arc geometry, and the chain of walls that follows it, on the breeze-block house.</summary>
static class ArcWallChecks
{
    /// <summary>Runs the geometry checks and then the session checks on a copy of the house.</summary>
    public static void Run(Action<bool, string> check)
    {
        Geometry(check);
        Session(check);
    }

    private static bool Near(double first, double second, double tolerance = 1e-9) => Math.Abs(first - second) < tolerance;

    private static bool Throws(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return true; }
        return false;
    }

    /// <summary>The circle through three points, in both directions, and the chain that follows it.</summary>
    private static void Geometry(Action<bool, string> check)
    {
        var quarter = ArcWallGeometry.Fit(new Point2(5, 0), new Point2(0, 5), new Point2(5 * Math.Cos(Math.PI / 4), 5 * Math.Sin(Math.PI / 4)));
        check(quarter is { } q && Near(q.Radius, 5) && Near(q.Center.X, 0) && Near(q.Center.Y, 0) && Near(q.SweepAngle, Math.PI / 2),
            "Three points on a quarter circle give its center, its radius and a counter-clockwise sweep of 90°");

        var major = ArcWallGeometry.Fit(new Point2(5, 0), new Point2(0, 5), new Point2(-5 * Math.Cos(Math.PI / 4), -5 * Math.Sin(Math.PI / 4)));
        check(major is { } m && Near(m.Radius, 5) && Near(m.SweepAngle, -3 * Math.PI / 2),
            "The same ends with the third point on the other side go the long way round, clockwise");

        check(ArcWallGeometry.Fit(new Point2(0, 0), new Point2(4, 0), new Point2(2, 0)) is null &&
              ArcWallGeometry.Fit(new Point2(0, 0), new Point2(4, 0), new Point2(2, 1e-7)) is null &&
              ArcWallGeometry.Fit(new Point2(0, 0), new Point2(0, 0), new Point2(1, 1)) is null,
            "Points on a line, or on a circle too large to tell from one, are not an arc");

        var shifted = ArcWallGeometry.Fit(new Point2(1_000_005, 2_000_000), new Point2(1_000_000, 2_000_005),
            new Point2(1_000_000 + 5 * Math.Cos(Math.PI / 4), 2_000_000 + 5 * Math.Sin(Math.PI / 4)));
        check(shifted is { } s && Near(s.Radius, 5, 1e-6) && Near(s.Center.X, 1_000_000, 1e-6),
            "An arc a million meters from the origin keeps its radius and center");

        var arc = quarter!.Value;
        var points = ArcWallGeometry.Points(arc, new Point2(5, 0), new Point2(0, 5));
        check(points[0] == new Point2(5, 0) && points[^1] == new Point2(0, 5) &&
              points.All(point => Near(Math.Sqrt(point.X * point.X + point.Y * point.Y), 5, 1e-9)),
            $"The chain starts and ends exactly where it was drawn, and every corner lies on the arc ({points.Count - 1} pieces)");
        var worst = Enumerable.Range(0, points.Count - 1).Max(index =>
            5 - Math.Sqrt(Math.Pow((points[index].X + points[index + 1].X) / 2, 2) + Math.Pow((points[index].Y + points[index + 1].Y) / 2, 2)));
        check(worst <= ArcWallGeometry.DefaultTolerance + 1e-9, $"No piece strays from the arc by more than {worst * 1000:0.0} mm");
        var length = Enumerable.Range(0, points.Count - 1).Sum(index => points[index].DistanceTo(points[index + 1]));
        check(length <= arc.Length && (arc.Length - length) / arc.Length < 0.005,
            $"The chain is slightly shorter than the arc, by {(arc.Length - length) / arc.Length * 100:0.00} %, never longer");
        check(ArcWallGeometry.Points(arc, new Point2(5, 0), new Point2(0, 5), 0.0005).Count > points.Count,
            "A tighter tolerance makes a finer chain");
    }

    /// <summary>A curved wall on the house: one step, one wall, joined, valid, and copies of its own.</summary>
    private static void Session(Action<bool, string> check)
    {
        var session = new DocumentSession(BreezeBlockHouseShowcase.Create());
        var level = session.Document.Levels[0].Id;
        var walls = session.Document.Walls.Count;
        var steps = session.UndoNames.Count;

        var ids = session.AddArcWall(level, new Point2(520, 500), new Point2(530, 510),
            new Point2(520 + 10 - 10 * Math.Cos(Math.PI / 4) + 0.0, 500 + 10 * Math.Sin(Math.PI / 4)));
        var pieces = session.Document.Walls.Where(wall => ids.Contains(wall.Id)).ToArray();
        check(ids.Count >= 6 && session.Document.Walls.Count == walls + ids.Count && session.UndoNames.Count == steps + 1,
            $"A curved wall of {ids.Count} pieces is added in one undoable step");
        check(pieces.Zip(pieces.Skip(1)).All(pair => pair.First.End == pair.Second.Start) &&
              pieces.Select(piece => piece.CurveGroupId).Distinct().Count() == 1 && pieces.All(piece => piece.CurveRadius > 0),
            "The pieces meet exactly end to end and share one curve identity");

        var index = WallJoinIndex.Build(session.Document.Walls);
        check(pieces.Zip(pieces.Skip(1)).All(pair => index.JoinAt(pair.First.Id, WallEnd.End) is { } join && join.Other.Id == pair.Second.Id),
            "Every corner between two pieces is a joined, mitered corner");
        check(BuildingMesher.Build(session.Document).Positions.All(position => float.IsFinite(position.X)),
            "The house with its curved wall still meshes");

        var longest = pieces.OrderByDescending(piece => piece.Length).First();
        var door = session.AddOpening(longest.Id, OpeningKind.Door, offset: longest.Length / 2);
        check(session.Document.Openings.Any(opening => opening.Id == door),
            $"A door fits in a piece of the curved wall ({longest.Length:0.00} m long)");

        // Copies of a curved wall are curved walls of their own.
        var copies = session.ArrayWallsLinear(ids.ToArray(), 2, 0, 15);
        var copyPieces = session.Document.Walls.Where(wall => copies.Contains(wall.Id)).ToArray();
        check(copyPieces.Length == ids.Count && copyPieces.Select(piece => piece.CurveGroupId).Distinct().Count() == 1 &&
              copyPieces[0].CurveGroupId != pieces[0].CurveGroupId,
            "A copy of a curved wall has a curve identity of its own");
        session.RemoveWall(copyPieces[0].Id);
        check(session.Document.Walls.Count(wall => copies.Contains(wall.Id)) == 0 && session.Document.Walls.Count(wall => ids.Contains(wall.Id)) == ids.Count,
            "Deleting one piece of the copy deletes the whole copy and leaves the original");

        // The pieces travel and disappear together, with their doors.
        var before = session.Document.Walls.Where(wall => ids.Contains(wall.Id)).ToDictionary(wall => wall.Id);
        session.MoveWall(ids[1], 2, -3);
        check(session.Document.Walls.Where(wall => ids.Contains(wall.Id)).All(wall =>
                  Near(wall.Start.X, before[wall.Id].Start.X + 2) && Near(wall.End.Y, before[wall.Id].End.Y - 3)),
            "Moving one piece moves the whole curved wall");
        session.RemoveWall(ids[2]);
        check(!session.Document.Walls.Any(wall => ids.Contains(wall.Id)) && session.Document.Openings.All(opening => opening.Id != door),
            "Deleting one piece deletes the whole curved wall with its door");
        session.Undo();
        check(session.Document.Walls.Count(wall => ids.Contains(wall.Id)) == ids.Count, "Undo brings the whole curved wall back");

        check(Throws(() => session.AddArcWall(level, new Point2(0, 0), new Point2(4, 0), new Point2(2, 0))) &&
              Throws(() => session.AddArcWall(level, new Point2(0, 0), new Point2(0.1, 0), new Point2(0.05, 0.05))) &&
              Throws(() => session.AddArcWall(level, new Point2(0, 0), new Point2(4, 0), new Point2(2, 1), wallTypeId: Guid.NewGuid())) &&
              Throws(() => session.AddArcWall(level, new Point2(0, 0), new Point2(4, 0), new Point2(2, 1), height: double.NaN)),
            "Points on a line, an arc tighter than the wall, an unknown type and a bad height are refused");
    }
}
