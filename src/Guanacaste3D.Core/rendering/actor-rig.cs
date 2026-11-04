using System.Numerics;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// A car made of a mesh family (the shipped sedan and coupé): its triangles, vertex normals and surface kinds, with the
/// middle of its floor at the origin and its length along Y. It drives as a rigid body; paint is the only thing a look
/// changes, so the same mesh serves several colours.
/// </summary>
internal sealed class CarTemplate
{
    /// <summary>Whether the car's nose points toward +Y in the mesh (+1) or toward −Y (−1); the shipped cars face −Y.</summary>
    public const float Forward = -1f;

    /// <summary>The model and paint of each of the six car looks; a null paint keeps the model's own.</summary>
    private static readonly (int Model, Vector3? Paint)[] Looks =
    [
        (0, new Vector3(0.62f, 0.62f, 0.60f)), (0, new Vector3(0.012f, 0.012f, 0.014f)), (0, new Vector3(0.42f, 0.025f, 0.025f)),
        (1, null), (1, new Vector3(0.025f, 0.07f, 0.34f)), (0, new Vector3(0.03f, 0.20f, 0.08f))
    ];

    public const int Models = 2;

    public static int ModelOf(int look) => Looks[Math.Clamp(look, 0, Actor.Looks - 1)].Model;

    public Vector3[] Positions { get; }
    public Vector3[] Normals { get; }
    public int[] Indices { get; }
    public SurfaceKind[] Kinds { get; }

    public CarTemplate(FamilyMesh.Decoded mesh)
    {
        var centre = new Vector3((mesh.Min.X + mesh.Max.X) / 2, (mesh.Min.Y + mesh.Max.Y) / 2, mesh.Min.Z);
        Positions = mesh.Positions.Select(point => point - centre).ToArray();
        Normals = mesh.Normals ?? new Vector3[mesh.Positions.Length];
        Indices = mesh.Indices;
        Kinds = mesh.Kinds;
    }

    /// <summary>The materials of a look, one for each surface kind the model uses (in the order of <see cref="KindOrder"/>).</summary>
    public RenderMaterial[] Materials(int look)
    {
        var paint = Looks[Math.Clamp(look, 0, Actor.Looks - 1)].Paint;
        return KindOrder().Select(kind =>
        {
            var material = MaterialLibrary.ForKind(kind);
            return paint is { } color && kind is SurfaceKind.CarPaint or SurfaceKind.CarPaintSilver ? material with { BaseColor = color } : material;
        }).ToArray();
    }

    public List<SurfaceKind> KindOrder() => Kinds.Distinct().Order().ToList();

    /// <summary>Writes the car at <paramref name="state"/> into <paramref name="positions"/> and <paramref name="normals"/>.</summary>
    public void Pose(ActorState state, Span<Vector3> positions, Span<Vector3> normals)
    {
        var yaw = state.Heading - Forward * MathF.PI / 2;
        float cos = MathF.Cos(yaw), sin = MathF.Sin(yaw);
        for (var index = 0; index < Positions.Length; index++)
        {
            var (local, normal) = (Positions[index], Normals[index]);
            positions[index] = state.Position + new Vector3(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos, local.Z);
            normals[index] = new Vector3(normal.X * cos - normal.Y * sin, normal.X * sin + normal.Y * cos, normal.Z);
        }
    }
}

/// <summary>
/// All the actors of a project as one block of dynamic geometry: the triangles and materials are fixed, and
/// <see cref="Frame"/> gives the vertices (and, for cars, their smooth normals) at any time of the movie. A scene is built
/// with the actors at their start, and a movie moves them frame by frame. Cars need their meshes, which the caller supplies.
/// </summary>
public sealed class ActorRig
{
    private readonly List<(Actor Actor, int Offset, int Count, CarTemplate? Car)> _blocks = [];

    private ActorRig(IReadOnlyList<Actor> actors, Func<int, FamilyMesh.Decoded?>? carModel)
    {
        var indices = new List<int>();
        var triangleMaterial = new List<int>();
        var materials = new List<RenderMaterial>();
        var templates = new Dictionary<int, CarTemplate?>();
        var offset = 0;
        foreach (var actor in actors)
        {
            var materialOffset = materials.Count;
            if (actor.Kind == ActorKind.Person)
            {
                indices.AddRange(PersonRig.Indices.Select(value => value + offset));
                materials.AddRange(PersonRig.Materials(actor.Look));
                triangleMaterial.AddRange(PersonRig.TriangleMaterial.Select(value => value + materialOffset));
                _blocks.Add((actor, offset, PersonRig.VertexCount, null));
                offset += PersonRig.VertexCount;
                continue;
            }
            var model = CarTemplate.ModelOf(actor.Look);
            if (!templates.TryGetValue(model, out var template))
                templates[model] = template = carModel?.Invoke(model) is { } decoded ? new CarTemplate(decoded) : null;
            if (template is null) continue;
            var order = template.KindOrder();
            indices.AddRange(template.Indices.Select(value => value + offset));
            materials.AddRange(template.Materials(actor.Look));
            triangleMaterial.AddRange(Enumerable.Range(0, template.Kinds.Length).Select(triangle => order.IndexOf(template.Kinds[triangle]) + materialOffset));
            _blocks.Add((actor, offset, template.Positions.Length, template));
            offset += template.Positions.Length;
            HasNormals = true;
        }
        Indices = [.. indices];
        TriangleMaterial = [.. triangleMaterial];
        Materials = [.. materials];
        VertexCount = offset;
    }

    /// <summary>A rig for the actors of a document, or null when the project has none that can be drawn.</summary>
    public static ActorRig? Of(BuildingDocument document, Func<int, FamilyMesh.Decoded?>? carModel = null) => From(Actors.Of(document), carModel);

    /// <summary>
    /// A rig for some actors. <paramref name="carModel"/> gives the mesh of a car model (0 sedan, 1 coupé); without it
    /// cars are left out. Null when no actor can be drawn.
    /// </summary>
    public static ActorRig? From(IReadOnlyList<Actor> actors, Func<int, FamilyMesh.Decoded?>? carModel = null)
    {
        var rig = new ActorRig(actors, carModel);
        return rig.VertexCount == 0 ? null : rig;
    }

    public int[] Indices { get; }
    public int[] TriangleMaterial { get; }
    public RenderMaterial[] Materials { get; }
    public int VertexCount { get; }

    /// <summary>Whether some actor has smooth vertex normals that must turn with it (every car does).</summary>
    public bool HasNormals { get; private set; }

    /// <summary>The number of actors the rig draws (cars without a mesh are left out).</summary>
    public int Count => _blocks.Count;

    /// <summary>Every vertex, and every smooth normal (zero for a flat person), of every actor at a time of the movie.</summary>
    public (Vector3[] Positions, Vector3[] Normals) Frame(double seconds)
    {
        var positions = new Vector3[VertexCount];
        var normals = new Vector3[VertexCount];
        foreach (var (actor, offset, count, car) in _blocks)
        {
            var state = Actors.At(actor, seconds);
            if (car is null) PersonRig.Pose(actor.Look, state, positions.AsSpan(offset, count));
            else car.Pose(state, positions.AsSpan(offset, count), normals.AsSpan(offset, count));
        }
        return (positions, normals);
    }

    /// <summary>Every vertex of every actor at a time of the movie.</summary>
    public Vector3[] Positions(double seconds) => Frame(seconds).Positions;
}
