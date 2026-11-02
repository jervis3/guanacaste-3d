using System.Reflection;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Desktop;

internal static partial class InteractionChecks
{
    /// <summary>Large architectural models must not allocate unbounded WPF preview faces.</summary>
    private static void ProjectThumbnailBudgetChecks()
    {
        var thumbnail = typeof(MainWindow).Assembly.GetType("Guanacaste3D.Desktop.ProjectThumbnail", throwOnError: true)!;
        var select = thumbnail.GetMethod("SelectTriangleIndices", BindingFlags.Static | BindingFlags.NonPublic)!;

        IReadOnlyList<int> Select(SurfaceKind[] kinds) =>
            (IReadOnlyList<int>)select.Invoke(null, [kinds])!;

        var architecture = Enumerable.Repeat(SurfaceKind.WallConcrete, 120_000).ToArray();
        var architecturalFaces = Select(architecture);
        Check(architecturalFaces.Count == 40_000 && architecturalFaces[0] == 0 &&
              architecturalFaces[^1] == 119_997,
            "Dense architecture is sampled uniformly to a strict 40,000-face preview budget");

        var withDecorations = Enumerable.Repeat(SurfaceKind.Leaf, 100_000)
            .Concat(Enumerable.Repeat(SurfaceKind.WallConcrete, 200))
            .Concat(Enumerable.Repeat(SurfaceKind.Ground, 50_000)).ToArray();
        var decoratedFaces = Select(withDecorations);
        Check(decoratedFaces.Count <= 40_000 &&
              decoratedFaces.Count(index => withDecorations[index] == SurfaceKind.Leaf) <= 3_500 &&
              decoratedFaces.Count(index => withDecorations[index] == SurfaceKind.WallConcrete) == 200 &&
              decoratedFaces.All(index => withDecorations[index] != SurfaceKind.Ground),
            "Decorative geometry is reduced without losing small architectural elements or including terrain");

        var denseMixed = Enumerable.Repeat(SurfaceKind.WallConcrete, 120_000)
            .Concat(Enumerable.Repeat(SurfaceKind.Leaf, 100_000)).ToArray();
        var mixedFaces = Select(denseMixed);
        Check(mixedFaces.Count <= 40_000 && mixedFaces.Any(index => denseMixed[index] == SurfaceKind.Leaf) &&
              mixedFaces.Any(index => denseMixed[index] == SurfaceKind.WallConcrete),
            "Mixed high-density models remain bounded and retain architecture and decorative context");
    }
}
