using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>
/// Lumion-style material balls: every surface row and every library tile shows a lit sphere that answers to the
/// material's colour, texture, roughness, metalness and relief, so a material is judged by sight and not by name.
/// </summary>
public partial class RenderPanel
{
    private int _materialBallRevision;

    private static float BallLinearToSrgb(float linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        return linear <= .0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1 / 2.4f) - .055f;
    }

    private static float BallSrgbToLinear(byte value)
    {
        var srgb = value / 255f;
        return srgb <= .04045f ? srgb / 12.92f : MathF.Pow((srgb + .055f) / 1.055f, 2.4f);
    }

    /// <summary>A plain-colour image the sphere shader can wrap around the ball when a material has no picture.</summary>
    private static BitmapSource SolidSwatch(Vector3 linear)
    {
        var b = (byte)Math.Round(BallLinearToSrgb(linear.Z) * 255);
        var g = (byte)Math.Round(BallLinearToSrgb(linear.Y) * 255);
        var r = (byte)Math.Round(BallLinearToSrgb(linear.X) * 255);
        var pixels = new byte[16];
        for (var i = 0; i < 4; i++) { pixels[i * 4] = b; pixels[i * 4 + 1] = g; pixels[i * 4 + 2] = r; pixels[i * 4 + 3] = 255; }
        var swatch = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
        swatch.Freeze();
        return swatch;
    }

    private static Vector3 TintToLinear(string tintHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(tintHex);
        return new Vector3(BallSrgbToLinear(color.R), BallSrgbToLinear(color.G), BallSrgbToLinear(color.B));
    }

    /// <summary>The ball of a library material that has no photograph: its flat colour, glossiness and metal.</summary>
    private static BitmapSource SolidMaterialBall(RenderMaterial material, double roughness, double metallic,
        double relief, string? tintHex)
    {
        var color = tintHex is not null ? TintToLinear(tintHex) : material.BaseColor;
        // Glass has no colour of its own: show it pale and clear, as Lumion does, instead of a black ball.
        if (material.Transmission > .5f)
            color = Vector3.Lerp(new Vector3(.9f), material.TransmissionTint, .5f);
        return MaterialSpherePreview.Create(SolidSwatch(color), roughness, metallic, relief);
    }

    /// <summary>
    /// Builds the ball of one surface row. Runs on a worker: it reads the shipped texture or package picture when
    /// there is one, and falls back to the flat colour of the preset or of the stock material of that surface.
    /// </summary>
    private static BitmapSource? RenderMaterialBall(RenderMaterialAssignment? assignment, SurfaceKind category,
        string? projectFile, double roughness, double metallic, double relief)
    {
        try
        {
            var stock = MaterialLibrary.ForKind(category);
            var preset = assignment is null ? null : MaterialPresets.FromPath(assignment.TexturePath);
            var file = assignment is null || assignment.TexturePath == "stock:"
                ? null : RenderTextureLibrary.Resolve(assignment.TexturePath, projectFile);
            if (file is not null && File.Exists(file))
                return MaterialSpherePreview.Create(DecodeGalleryImage(file, 192), roughness, metallic, relief);
            return SolidMaterialBall(preset?.Material ?? stock, roughness, metallic, relief, assignment?.TintHex);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                            NotSupportedException or ArgumentException or FormatException or
                                            InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    /// <summary>The picture frame of a row: a dark well that fills with the ball once it has been drawn.</summary>
    private Border BuildMaterialBall(RenderMaterialAssignment? assignment, SurfaceKind category, int revision)
    {
        var original = GalleryOriginalAppearance(assignment, category);
        var roughness = assignment?.RoughnessOverride ?? original.Roughness;
        var metallic = assignment?.MetallicOverride ?? original.Metallic;
        var relief = assignment?.Relief ?? 0;
        var picture = new Image { Stretch = Stretch.Uniform, IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
        var frame = new Border
        {
            Width = 100, Height = 64, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true,
            Background = new SolidColorBrush(Color.FromRgb(24, 36, 49)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(62, 83, 101)), BorderThickness = new Thickness(1),
            Child = picture,
            ToolTip = $"Bola de ejemplo · rugosidad {roughness:0.00} · metálico {metallic:0.00}"
        };
        var projectFile = _source?.ProjectFilePath;
        _ = LoadMaterialBallAsync(picture, revision, () =>
            RenderMaterialBall(assignment, category, projectFile, roughness, metallic, relief));
        return frame;
    }

    private async Task LoadMaterialBallAsync(Image view, int revision, Func<BitmapSource?> make)
    {
        var ball = await Task.Run(make);
        // A newer rebuild of the rows replaced this one while it was drawing.
        if (revision == _materialBallRevision && ball is not null) view.Source = ball;
    }

    private const int OptionBallCacheLimit = 240;
    private readonly Dictionary<string, BitmapSource> _optionBalls = new(StringComparer.Ordinal);

    /// <summary>
    /// One entry of the material drop-down: a small ball and the name. The ball is drawn when the entry scrolls
    /// into view (the list is virtualized), so opening a list of hundreds of materials stays instant.
    /// </summary>
    private FrameworkElement BuildOptionContent(string name, string path, SurfaceKind category,
        double roughness, double metallic, double relief)
    {
        var picture = new Image { Width = 56, Height = 36, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
        var grid = new Grid { MinHeight = 38 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(picture, 0);
        grid.Children.Add(picture);
        grid.Children.Add(new TextBlock
        {
            Text = name, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4, 0, 4, 0)
        });
        Grid.SetColumn((UIElement)grid.Children[1], 1);
        picture.Loaded += async (_, _) =>
        {
            if (picture.Source is not null) return;
            if (_optionBalls.TryGetValue(path, out var cached)) { picture.Source = cached; return; }
            var projectFile = _source?.ProjectFilePath;
            var ball = await Task.Run(() => RenderMaterialBall(new RenderMaterialAssignment(category, path),
                category, projectFile, roughness, metallic, relief));
            if (ball is null) return;
            if (_optionBalls.Count >= OptionBallCacheLimit) _optionBalls.Clear();
            _optionBalls[path] = ball;
            picture.Source = ball;
        };
        return grid;
    }
}
