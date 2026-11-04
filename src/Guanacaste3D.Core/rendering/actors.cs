using System.Numerics;
using System.Text.Json;

namespace Guanacaste3D.Core.Rendering;

/// <summary>A point of an actor's route, in project metres: where the feet touch the ground.</summary>
public readonly record struct ActorPoint(double X, double Y, double Z);

/// <summary>
/// A person of the render who walks a route, as in Lumion's animated people. <see cref="Look"/> chooses skin, clothes and
/// height (0–5); <see cref="Speed"/> is in metres per second; with <see cref="Loop"/> the person walks back and forth, and
/// without it stops at the last point. <see cref="Start"/> is the second of the movie at which the person sets off; before
/// that, and with a single point, the person stands still.
/// </summary>
public sealed record Actor(Guid Id, string Name, int Look, double Speed, bool Loop, double Start, IReadOnlyList<ActorPoint> Route)
{
    public const int Looks = 6;
    public const int MaximumRoutePoints = 200;

    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 80 && Look is >= 0 and < Looks &&
        double.IsFinite(Speed) && Speed is >= 0.1 and <= 5 && double.IsFinite(Start) && Start is >= 0 and <= 36000 &&
        Route.Count is >= 1 and <= MaximumRoutePoints &&
        Route.All(point => double.IsFinite(point.X + point.Y + point.Z) && Math.Abs(point.X) < 1e6 && Math.Abs(point.Y) < 1e6 && Math.Abs(point.Z) < 1e5);
}

/// <summary>Where an actor is, and how it is walking, at a time of the movie.</summary>
public readonly record struct ActorState(Vector3 Position, float Heading, float Distance, bool Walking);

/// <summary>The route of an actor followed over time, and the actors as the text the project keeps.</summary>
public static class Actors
{
    public const string Key = "render.actors";
    public const int Maximum = 100;

    /// <summary>
    /// The actor's place at a time: it follows its route at its speed from <see cref="Actor.Start"/>, stands at the first
    /// point before that, and either stops at the end or turns round and walks back (a looping actor). The heading is the
    /// direction of walking, in radians from the +X axis.
    /// </summary>
    public static ActorState At(Actor actor, double seconds)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var route = actor.Route;
        var first = new Vector3((float)route[0].X, (float)route[0].Y, (float)route[0].Z);
        double length = 0;
        for (var index = 1; index < route.Count; index++) length += Flat(route[index - 1], route[index]);
        if (route.Count == 1 || length < 1e-3)
            return new ActorState(first, 0, 0, false);

        var walked = Math.Max(0, seconds - actor.Start) * actor.Speed;
        var reversed = false;
        double along;
        bool walking;
        if (actor.Loop)
        {
            var cycle = 2 * length;
            var inCycle = walked % cycle;
            reversed = inCycle > length;
            along = reversed ? cycle - inCycle : inCycle;
            walking = walked > 0;
        }
        else
        {
            along = Math.Min(walked, length);
            walking = walked > 0 && walked < length;
        }

        double covered = 0;
        for (var index = 1; index < route.Count; index++)
        {
            var segment = Flat(route[index - 1], route[index]);
            if (segment < 1e-6) continue;
            if (along <= covered + segment || index == route.Count - 1)
            {
                var t = Math.Clamp((along - covered) / segment, 0, 1);
                var (a, b) = (route[index - 1], route[index]);
                var position = new Vector3((float)(a.X + (b.X - a.X) * t), (float)(a.Y + (b.Y - a.Y) * t), (float)(a.Z + (b.Z - a.Z) * t));
                var heading = (float)Math.Atan2(b.Y - a.Y, b.X - a.X);
                if (reversed) heading += MathF.PI;
                return new ActorState(position, heading, (float)walked, walking);
            }
            covered += segment;
        }
        return new ActorState(first, 0, 0, false);
    }

    private static double Flat(ActorPoint a, ActorPoint b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    public static string? Serialize(IReadOnlyCollection<Actor> actors) =>
        actors.Count == 0 ? null : JsonSerializer.Serialize(new
        {
            V = 1,
            Items = actors.Take(Maximum).Select(actor => new
            {
                actor.Id, actor.Name, actor.Look, actor.Speed, actor.Loop, actor.Start,
                Route = actor.Route.Select(point => new[] { point.X, point.Y, point.Z })
            })
        });

    /// <summary>Reads saved actors; null when the text is damaged, an empty list when there are none.</summary>
    public static IReadOnlyList<Actor>? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.GetProperty("V").GetInt32() != 1) return null;
            var items = root.GetProperty("Items");
            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > Maximum) return null;
            var actors = new List<Actor>();
            foreach (var item in items.EnumerateArray())
            {
                var route = new List<ActorPoint>();
                foreach (var point in item.GetProperty("Route").EnumerateArray())
                {
                    if (point.GetArrayLength() != 3) return null;
                    route.Add(new ActorPoint(point[0].GetDouble(), point[1].GetDouble(), point[2].GetDouble()));
                    if (route.Count > Actor.MaximumRoutePoints) return null;
                }
                var actor = new Actor(item.GetProperty("Id").GetGuid(), item.GetProperty("Name").GetString() ?? "",
                    item.GetProperty("Look").GetInt32(), item.GetProperty("Speed").GetDouble(), item.GetProperty("Loop").GetBoolean(),
                    item.GetProperty("Start").GetDouble(), route);
                if (!actor.IsValid) return null;
                actors.Add(actor);
            }
            return actors;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The actors saved in a document; empty when there are none or the text cannot be read.</summary>
    public static IReadOnlyList<Actor> Of(BuildingDocument document) =>
        TryParse(document.Extensions.GetValueOrDefault(Key)) ?? [];
}
