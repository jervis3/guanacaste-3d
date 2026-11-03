using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>
    /// Runs the wall join, pin, flip, copy, cut and paste ribbon commands through the window's real command router
    /// on walls placed away from the other checks, and removes everything it created.
    /// </summary>
    private static void WallEditCommandChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var first = session.AddWall(level, new Point2(200, 200), new Point2(206, 200));
        var second = session.AddWall(level, new Point2(206, 200), new Point2(206, 203));
        var created = new List<Guid> { first, second };
        Wall Find(Guid id) => session.Document.Walls.Single(wall => wall.Id == id);
        void Select(Guid id) => SetField("_selectedWallId", (Guid?)id);

        Select(first);
        Invoke("ExecuteApplicationCommand", "DisallowJoin");
        Check(Find(first).StartJoin == WallJoinSetting.Disallowed && Find(first).EndJoin == WallJoinSetting.Disallowed,
            "The Disallow Join ribbon command releases both ends of the selected wall");
        Invoke("ExecuteApplicationCommand", "ButtJoin");
        Check(Find(first).EndJoin == WallJoinSetting.Butt, "The Butt Join ribbon command makes the selected wall butt");
        Invoke("ExecuteApplicationCommand", "AllowJoin");
        Check(Find(first).StartJoin == WallJoinSetting.Miter && Find(first).EndJoin == WallJoinSetting.Miter,
            "The Allow Join ribbon command restores mitered corners");

        Invoke("ExecuteApplicationCommand", "Pin");
        Check(Find(first).Pinned, "The Pin ribbon command pins the selected wall");
        Invoke("ExecuteApplicationCommand", "Pin");
        Check(!Find(first).Pinned, "The Pin ribbon command toggles the pin off again");

        var start = Find(first).Start;
        Invoke("ExecuteApplicationCommand", "FlipWall");
        Check(Find(first).End == start, "The Flip Wall ribbon command swaps the ends of the selected wall");
        Invoke("ExecuteApplicationCommand", "FlipWall");
        Check(Find(first).Start == start, "Flipping a second time restores the wall");

        var before = session.Document.Walls.Count;
        Invoke("ExecuteApplicationCommand", "Paste");
        Check(session.Document.Walls.Count == before, "Pasting with an empty clipboard changes nothing");
        Invoke("ExecuteApplicationCommand", "Copy");
        Invoke("ExecuteApplicationCommand", "Paste");
        Check(session.Document.Walls.Count == before + 1, "Copy and Paste ribbon commands add one wall");
        var pasted = Field<Guid?>("_selectedWallId")!.Value;
        created.Add(pasted);
        Check(pasted != first && Find(pasted).Start.DistanceTo(Find(first).Start) > 0.5,
            "The pasted wall is selected and sits apart from its source");

        Invoke("ExecuteApplicationCommand", "Cut");
        Check(session.Document.Walls.Count == before && session.Document.Walls.All(wall => wall.Id != pasted),
            "The Cut ribbon command removes the selected wall");
        Invoke("ExecuteApplicationCommand", "Paste");
        var restored = Field<Guid?>("_selectedWallId")!.Value;
        created.Add(restored);
        Check(session.Document.Walls.Count == before + 1, "A cut wall can be pasted back");

        Select(first);
        Invoke("ExecuteApplicationCommand", "ReviewWarningsUnavailable");
        Check(Field<Guid?>("_selectedWallId") == first, "An unknown ribbon command leaves the selection alone");

        foreach (var id in created.Where(id => session.Document.Walls.Any(wall => wall.Id == id)))
            session.RemoveWall(id);
        SetField("_selectedWallId", null);
        Invoke("RenderPlan");
        Pump();
    }
}
