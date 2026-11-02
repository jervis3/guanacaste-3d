using System.Buffers.Binary;
using System.IO;
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

        ProjectThumbnailCacheLoadChecks(thumbnail);
    }

    /// <summary>Corrupt or unexpectedly large cached images must never break the Home screen.</summary>
    private static void ProjectThumbnailCacheLoadChecks(Type thumbnail)
    {
        var load = thumbnail.GetMethod("Load", BindingFlags.Static | BindingFlags.Public)!;
        object? Load(string path) => load.Invoke(null, [path]);

        var directory = Path.Combine(Path.GetTempPath(), "guanacaste-thumbnail-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Check(Load(Path.Combine(directory, "missing.png")) is null,
                "Missing cached thumbnail is ignored");

            var damaged = Path.Combine(directory, "damaged.png");
            File.WriteAllBytes(damaged, [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82]);
            Check(Load(damaged) is null,
                "Truncated cached PNG is ignored instead of crashing the Home screen");

            var oversized = Path.Combine(directory, "oversized.png");
            using (var stream = File.Create(oversized))
                stream.SetLength(16L * 1024 * 1024 + 1);
            Check(Load(oversized) is null,
                "Unexpectedly large cached PNG is rejected before decoding");

            // A tiny file can declare an enormous decoded bitmap; reject it before WPF loads it.
            static byte[] PngHeader(uint width, uint height)
            {
                var header = new byte[24];
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(header, 0);
                header[11] = 13; // IHDR chunk length (big-endian)
                "IHDR"u8.CopyTo(header.AsSpan(12));
                BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16, 4), width);
                BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20, 4), height);
                return header;
            }

            var hugeDimensions = Path.Combine(directory, "huge-dimensions.png");
            File.WriteAllBytes(hugeDimensions, PngHeader(100_000, 100_000));
            Check(Load(hugeDimensions) is null,
                "PNG with extreme decoded dimensions is rejected before WPF allocation");

            var zeroDimensions = Path.Combine(directory, "zero-dimensions.png");
            File.WriteAllBytes(zeroDimensions, PngHeader(0, 384));
            Check(Load(zeroDimensions) is null,
                "PNG with invalid zero dimensions is rejected");

            var fakePng = Path.Combine(directory, "fake.png");
            File.WriteAllBytes(fakePng, new byte[24]);
            Check(Load(fakePng) is null,
                "Non-PNG cache data is rejected before decoding");

            var valid = Path.Combine(directory, "valid.png");
            var source = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96,
                System.Windows.Media.PixelFormats.Pbgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
            using (var stream = File.Create(valid))
                encoder.Save(stream);
            Check(Load(valid) is System.Windows.Media.Imaging.BitmapSource,
                "Valid cached PNG still loads correctly");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
