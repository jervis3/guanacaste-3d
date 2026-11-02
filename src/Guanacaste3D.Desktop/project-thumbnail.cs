using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Desktop;

/// <summary>Creates compact, shaded isometric previews for the Home screen.</summary>
internal static class ProjectThumbnail
{
    public const int Width = 384;
    public const int Height = 384;
    private const int MaximumTriangles = 40000;

    /// <summary>A projected surface with a depth used to paint distant surfaces first.</summary>
    public readonly record struct Face(Point A, Point B, Point C, double Depth, Color Color);

    /// <summary>Builds a bounded preview mesh off the UI thread, omitting oversized terrain.</summary>
    public static IReadOnlyList<Face> Faces(BuildingDocument document)
    {
        var mesh = BuildingMesher.Build(document);
        var stride = Math.Max(1, (int)Math.Ceiling(mesh.TriangleCount / (double)MaximumTriangles));
        var faces = new List<Face>(Math.Min(mesh.TriangleCount, MaximumTriangles));
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle += stride)
        {
            var kind = mesh.TriangleKinds[triangle];
            if (kind is SurfaceKind.Ground or SurfaceKind.Earth) continue;
            var a = mesh.Positions[mesh.Indices[triangle * 3]];
            var b = mesh.Positions[mesh.Indices[triangle * 3 + 1]];
            var c = mesh.Positions[mesh.Indices[triangle * 3 + 2]];
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared() < 1e-12f) continue;
            normal = Vector3.Normalize(normal);
            var light = 0.68 + 0.32 * Math.Abs(Vector3.Dot(normal, Vector3.Normalize(new Vector3(-1, -2, 3))));
            var color = SurfaceColor(kind);
            color = Color.FromRgb((byte)(color.R * light), (byte)(color.G * light), (byte)(color.B * light));
            faces.Add(new Face(Project(a), Project(b), Project(c),
                (a.X + b.X + c.X + a.Y + b.Y + c.Y - a.Z - b.Z - c.Z) / 3, color));
        }
        faces.Sort((left, right) => right.Depth.CompareTo(left.Depth));
        return faces;
    }

    /// <summary>Projects model coordinates onto an isometric view.</summary>
    private static Point Project(Vector3 point) =>
        new((point.X - point.Y) * 0.70710678, -((point.X + point.Y) * 0.40824829 + point.Z * 0.81649658));

    /// <summary>Maps surface categories to a restrained architectural material palette.</summary>
    private static Color SurfaceColor(SurfaceKind kind) => kind switch
    {
        SurfaceKind.Foliage or SurfaceKind.Leaf => Color.FromRgb(104, 151, 98),
        SurfaceKind.Bark or SurfaceKind.WallWood or SurfaceKind.DoorLeaf => Color.FromRgb(152, 114, 82),
        SurfaceKind.Glass or SurfaceKind.TintedGlass or SurfaceKind.Mirror => Color.FromRgb(126, 164, 177),
        SurfaceKind.Water => Color.FromRgb(96, 157, 174),
        SurfaceKind.Roof or SurfaceKind.WallBrick => Color.FromRgb(169, 113, 91),
        SurfaceKind.Metal or SurfaceKind.WallMetal or SurfaceKind.Structure => Color.FromRgb(126, 139, 143),
        SurfaceKind.Asphalt or SurfaceKind.Rubber => Color.FromRgb(95, 106, 109),
        SurfaceKind.Floor or SurfaceKind.WallConcrete or SurfaceKind.WallBlock => Color.FromRgb(197, 199, 188),
        _ => Color.FromRgb(226, 224, 211)
    };

    /// <summary>Paints projected faces on the UI thread and fits the model to the card.</summary>
    public static BitmapSource Render(IReadOnlyList<Face> faces)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(232, 239, 235)), null, new Rect(0, 0, Width, Height));
            var points = faces.SelectMany(face => new[] { face.A, face.B, face.C }).ToArray();
            if (points.Length > 0)
            {
                var minX = points.Min(point => point.X); var maxX = points.Max(point => point.X);
                var minY = points.Min(point => point.Y); var maxY = points.Max(point => point.Y);
                var scale = Math.Min((Width - 28) / Math.Max(1e-6, maxX - minX), (Height - 28) / Math.Max(1e-6, maxY - minY));
                Point Map(Point point) => new(Width / 2 + (point.X - (minX + maxX) / 2) * scale, Height / 2 + (point.Y - (minY + maxY) / 2) * scale);
                var brushes = new Dictionary<Color, SolidColorBrush>();
                foreach (var face in faces)
                {
                    if (!brushes.TryGetValue(face.Color, out var brush))
                        brushes[face.Color] = brush = new SolidColorBrush(face.Color);
                    var geometry = new StreamGeometry();
                    using (var path = geometry.Open())
                    {
                        path.BeginFigure(Map(face.A), true, true);
                        path.LineTo(Map(face.B), true, false);
                        path.LineTo(Map(face.C), true, false);
                    }
                    geometry.Freeze();
                    context.DrawGeometry(brush, null, geometry);
                }
            }
        }
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Writes a preview as PNG, replacing an older cached image.</summary>
    public static void Save(BitmapSource image, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>Loads a cached PNG without locking it; invalid caches are ignored.</summary>
    public static BitmapSource? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
