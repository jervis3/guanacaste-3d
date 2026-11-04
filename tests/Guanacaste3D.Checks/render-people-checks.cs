using System.Numerics;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;

/// <summary>Checks the people of the render: their routes over time, their saved text, their bodies, and that a tracer moves them.</summary>
static class RenderPeopleChecks
{
    public static void Run(Action<bool, string> check)
    {
        // Routes over time.
        Actor Walker(bool loop, double start, params ActorPoint[] route) => new(Guid.NewGuid(), "Ana", 2, 2, loop, start, route);
        var straight = Walker(false, 1, new ActorPoint(0, 0, 0), new ActorPoint(10, 0, 0));
        var (before, early, middle, finished) = (Actors.At(straight, 0), Actors.At(straight, 1), Actors.At(straight, 3), Actors.At(straight, 9));
        check(!before.Walking && before.Position.X == 0 && !early.Walking && middle.Walking && Math.Abs(middle.Position.X - 4) < 1e-4 &&
              Math.Abs(middle.Heading) < 1e-4 && !finished.Walking && Math.Abs(finished.Position.X - 10) < 1e-4,
            "A person stands before it sets off, walks the route at its speed, and stops at the last point");
        var back = Walker(true, 1, new ActorPoint(0, 0, 0), new ActorPoint(10, 0, 0));
        var (atEnd, returning) = (Actors.At(back, 6), Actors.At(back, 8));
        check(Math.Abs(atEnd.Position.X - 10) < 1e-4 && Math.Abs(returning.Position.X - 6) < 1e-4 && Math.Abs(Math.Abs(returning.Heading) - Math.PI) < 1e-4 && returning.Walking,
            "A looping person turns round at the end and walks back, facing the way it goes");
        var corner = Walker(false, 0, new ActorPoint(0, 0, 0), new ActorPoint(4, 0, 0), new ActorPoint(4, 4, 1));
        var turned = Actors.At(corner, 3);
        check(Math.Abs(turned.Position.X - 4) < 1e-4 && Math.Abs(turned.Position.Y - 2) < 1e-4 && Math.Abs(turned.Heading - Math.PI / 2) < 1e-4 &&
              Math.Abs(turned.Position.Z - 0.5) < 1e-4,
            "A route with a corner turns the person, and the height follows the route");
        check(!Actors.At(Walker(false, 0, new ActorPoint(3, 4, 0)), 5).Walking && !Actors.At(Walker(false, 0, new ActorPoint(1, 1, 0), new ActorPoint(1, 1, 0)), 5).Walking,
            "A person with one point, or a route of no length, stands still");

        // Saved text.
        var actors = new List<Actor> { straight, back with { Look = 5, Name = "Luis" } };
        check(Actors.TryParse(Actors.Serialize(actors)) is { Count: 2 } read && read[0].Route.SequenceEqual(straight.Route) && read[1].Look == 5 && read[1].Loop &&
              Actors.Serialize([]) is null && Actors.TryParse(null)!.Count == 0,
            "People read back exactly from the text the project keeps, and none keep nothing");
        string[] bad = ["no es json", "{}", """{"V":2,"Items":[]}""",
            """{"V":1,"Items":[{"Id":"11111111-1111-1111-1111-111111111111","Name":"x","Look":9,"Speed":1,"Loop":false,"Start":0,"Route":[[0,0,0]]}]}""",
            """{"V":1,"Items":[{"Id":"11111111-1111-1111-1111-111111111111","Name":"x","Look":1,"Speed":0,"Loop":false,"Start":0,"Route":[[0,0,0]]}]}""",
            """{"V":1,"Items":[{"Id":"11111111-1111-1111-1111-111111111111","Name":"x","Look":1,"Speed":1,"Loop":false,"Start":0,"Route":[]}]}""",
            """{"V":1,"Items":[{"Id":"11111111-1111-1111-1111-111111111111","Name":"x","Look":1,"Speed":1,"Loop":false,"Start":0,"Route":[[0,0]]}]}"""];
        check(bad.All(item => Actors.TryParse(item) is null), "Damaged people text, an impossible look or speed, and a route with no points or a bad point are refused");

        // The body.
        var rig = ActorRig.From([straight])!;
        var standing = rig.Positions(0);
        check(rig.Indices.Length % 3 == 0 && rig.Indices.Max() < rig.VertexCount && rig.TriangleMaterial.Length == rig.Indices.Length / 3 &&
              rig.TriangleMaterial.Max() < rig.Materials.Length && rig.Indices.Length / 3 is > 200 and < 800 && standing.All(point => float.IsFinite(point.X + point.Y + point.Z)),
            $"A person is a few hundred consistent triangles ({rig.Indices.Length / 3}) with its five materials");
        var height = standing.Max(point => point.Z) - standing.Min(point => point.Z);
        check(Math.Abs(height - 1.815f * (0.90f + 0.018f * 2)) < 0.05f && Math.Abs(standing.Min(point => point.Z)) < 0.01f,
            $"A standing person is about as tall as a person and stands on the ground ({height:0.00} m)");
        var walking = rig.Positions(3);
        var swing = Enumerable.Range(0, standing.Length).Max(index => Vector3.Distance(walking[index] - Actors.At(straight, 3).Position, standing[index] - Actors.At(straight, 0).Position));
        check(swing > 0.05f, $"A walking person swings its legs and arms ({swing:0.00} m at most)");
        (float X, float Y) Extent(Vector3[] points) => (points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y));
        var facingX = Extent(ActorRig.From([Walker(false, 0, new ActorPoint(0, 0, 0), new ActorPoint(5, 0, 0))])!.Positions(0));
        var facingY = Extent(ActorRig.From([Walker(false, 0, new ActorPoint(0, 0, 0), new ActorPoint(0, 5, 0))])!.Positions(0));
        check(facingX.Y > facingX.X && facingY.X > facingY.Y, "A person walking along X is wide across Y, and walking along Y wide across X: it faces where it goes");
        check(Enumerable.Range(0, Actor.Looks).Select(look => ActorRig.From([straight with { Look = look }])!.Materials[1].BaseColor).Distinct().Count() == Actor.Looks,
            "Each of the six looks has its own clothes");

        // A tracer moves them without moving the scene it was given.
        var ground = new SceneMesh();
        ground.AddQuad(new(-100, -100, 0), new(100, -100, 0), new(100, 100, 0), new(-100, 100, 0), SurfaceKind.Ground);
        var standingAtOrigin = Walker(false, 0, new ActorPoint(0, 0, 0));
        var one = ActorRig.From([standingAtOrigin])!;
        var withPerson = new RenderSceneBuilder().AddBuilding(ground).AddDynamic(one.Positions(0), one.Indices, one.TriangleMaterial, one.Materials).Build();
        var empty = new RenderSceneBuilder().AddBuilding(ground).Build();
        var camera = new RenderCamera(new Vector3(0, -6, 1.5f), new Vector3(0, 0, 0.9f), 45);
        var settings = new RenderSettings { Seed = 3 };
        byte[] Shot(RenderScene scene, Action<PathTracer>? change = null)
        {
            var tracer = new PathTracer(scene, 96, 64, camera, settings);
            change?.Invoke(tracer);
            for (var pass = 0; pass < 3; pass++) tracer.RenderPass();
            return tracer.ToBgra32(finish: ImageProcessor.Plain);
        }
        // Pixels where the two pictures differ clearly: a person is a small figure, so a mean over the picture would hide it.
        double Difference(byte[] a, byte[] b) => Enumerable.Range(0, a.Length / 4).Count(pixel =>
            Enumerable.Range(0, 3).Any(channel => Math.Abs(a[pixel * 4 + channel] - b[pixel * 4 + channel]) > 25));
        var bare = Shot(empty);
        var there = Shot(withPerson);
        var gone = Shot(withPerson, tracer => tracer.MoveActors(one.Positions(0).Select(point => point + new Vector3(300, 0, 0)).ToArray()));
        check(withPerson.DynamicTriangleCount == one.Indices.Length / 3 && withPerson.DynamicVertexCount == one.VertexCount && empty.DynamicTriangleCount == 0 &&
              Difference(bare, there) > 40 && Difference(bare, gone) < 6,
            $"A person in the scene shows in the picture, and moved out of view leaves it as it was ({Difference(bare, there)} pixels differ with, {Difference(bare, gone)} moved away)");
        check(withPerson.Positions[withPerson.DynamicVertexStart] == one.Positions(0)[0],
            "Moving a person in one tracer leaves the shared scene as it was");
        var failures = 0;
        try { new PathTracer(empty, 8, 8, camera).MoveActors(new Vector3[3]); } catch (InvalidOperationException) { failures++; }
        try { new PathTracer(withPerson, 8, 8, camera).MoveActors(new Vector3[3]); } catch (ArgumentException) { failures++; }
        try { new RenderSceneBuilder().AddDynamic(one.Positions(0), one.Indices, one.TriangleMaterial, one.Materials).AddBuilding(ground); } catch (InvalidOperationException) { failures++; }
        check(failures == 3, "Moving people needs a scene that has them, the right number of vertices, and they must be added last");

        // The movie moves them frame by frame.
        var path = new CameraPath();
        path.Add(new CameraMark(camera.Position, camera.Target, 45, 0));
        path.Add(new CameraMark(camera.Position, camera.Target, 45, 2));
        var crossing = ActorRig.From([Walker(false, 0, new ActorPoint(-1.5, 0, 0), new ActorPoint(1.5, 0, 0)) with { Speed = 1.5 }])!;
        var crossingScene = new RenderSceneBuilder().AddBuilding(ground).AddDynamic(crossing.Positions(0), crossing.Indices, crossing.TriangleMaterial, crossing.Materials).Build();
        var folder = Path.Combine(Path.GetTempPath(), "guanacaste-people-" + Guid.NewGuid().ToString("N"));
        try
        {
            TourRenderer.RenderAsync(crossingScene, path, settings, 96, 64, 4, 3, new ImageFinish(), Path.Combine(folder, "walk"), null,
                CancellationToken.None, null, crossing).GetAwaiter().GetResult();
            TourRenderer.RenderAsync(crossingScene, path, settings, 96, 64, 4, 3, new ImageFinish(), Path.Combine(folder, "still"), null,
                CancellationToken.None).GetAwaiter().GetResult();
            var walk = Directory.GetFiles(Path.Combine(folder, "walk"), "frame_*.png").Order().ToArray();
            var still = Directory.GetFiles(Path.Combine(folder, "still"), "frame_*.png").Order().ToArray();
            double Frames(string a, string b)
            {
                var (x, y) = (PngCodec.Load(a).Rgb, PngCodec.Load(b).Rgb);
                return Enumerable.Range(0, x.Length / 3).Count(pixel => Enumerable.Range(0, 3).Any(channel => Math.Abs(x[pixel * 3 + channel] - y[pixel * 3 + channel]) > 25));
            }
            check(walk.Length == still.Length && walk.Length >= 8 && Frames(walk[0], walk[^1]) > Frames(still[0], still[^1]) + 40,
                $"In the video the camera is still and the person crosses the view ({Frames(walk[0], walk[^1])} pixels change against {Frames(still[0], still[^1])})");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        // The cobogó house keeps working with people in it, and reads none from a project without them.
        var house = BreezeBlockHouseShowcase.Create();
        check(ActorRig.Of(house) is null, "A project without people has no rig");
        house.Extensions[Actors.Key] = Actors.Serialize([standingAtOrigin])!;
        check(ActorRig.Of(house) is { VertexCount: > 100 } && Actors.Of(house).Count == 1, "A project's people are read from its extension store");
    }
}
