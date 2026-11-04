using System.Numerics;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// Blades of grass for the lawn pattern, found by marching the view ray through a few shells of the grass layer instead of
/// modelling millions of triangles: at each height the ray is over some point of the ground, and a blade is there when that
/// point lies inside the blade's width at that height. Blades are thick at the root and thin at the tip, lean a little each their
/// own way, differ in height, and shade from dark root to light tip, so tall grass has depth and parallax and a mown lawn
/// shows its stripes of blades. Cost is a few hash lookups per ground point that is seen sharply; it costs nothing where the grass
/// is off.
/// </summary>
internal static partial class SurfacePatterns
{
    private const int GrassLayers = 8;

    /// <summary>The average of <see cref="GrassBlades"/> tones over a lawn: what a far, unresolved lawn is multiplied by so it matches the near one.</summary>
    internal const float GrassMeanTone = 0.80f;

    /// <summary>What the view ray finds in the grass: the tone to multiply the lawn's colour by, how blades bend the normal there, and whether it is a blade.</summary>
    internal readonly record struct GrassSample(Vector3 Tone, Vector3 Bend, bool Blade);

    /// <summary>
    /// The grass at a point of the ground. <paramref name="height"/> is the tallest blade in metres, <paramref name="pitch"/> the
    /// spacing of blades in metres, <paramref name="geometric"/> the (upward) normal of the ground and <paramref name="direction"/>
    /// the direction the ray travels (toward the ground).
    /// </summary>
    internal static GrassSample GrassBlades(Vector3 position, Vector3 geometric, Vector3 direction, float height, float pitch)
    {
        var cosine = -Vector3.Dot(geometric, direction);
        var soil = new GrassSample(new Vector3(0.34f, 0.26f, 0.18f), Vector3.Zero, false);
        if (cosine < 0.04f || !(height > 1e-4f) || !(pitch > 1e-4f)) return soil;
        for (var layer = 0; layer < GrassLayers; layer++)
        {
            // From the tips down: the first blade the ray touches is the one seen.
            var fraction = 1f - (layer + 0.5f) / GrassLayers;
            var q = position - direction * (height * fraction / cosine);
            float u = q.X / pitch, v = q.Y / pitch;
            int cellX = (int)MathF.Floor(u), cellY = (int)MathF.Floor(v);
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int x = cellX + dx, y = cellY + dy;
                    float h1 = Hash01(x, y), h2 = Hash01(x + 911, y - 433), h3 = Hash01(x - 77, y + 5);
                    // Blades are not all as tall: the tips of the lawn are ragged.
                    var tall = 0.55f + 0.45f * h2;
                    if (fraction > tall) continue;
                    var angle = h3 * 2 * MathF.PI;
                    var lean = fraction * fraction * (0.5f + 0.9f * h1);
                    float cx = x + 0.15f + 0.7f * h1 + MathF.Cos(angle) * lean, cy = y + 0.15f + 0.7f * h2 + MathF.Sin(angle) * lean;
                    var radius = 0.04f + 0.36f * (1 - 0.8f * fraction / tall);
                    float ex = u - cx, ey = v - cy;
                    var squared = ex * ex + ey * ey;
                    if (squared >= radius * radius) continue;
                    // Dark at the root, light at the tip, a little warmer where it is tall, and each blade its own shade.
                    var shade = (0.40f + 1.25f * fraction / tall) * (0.82f + 0.36f * h3);
                    var warm = fraction / tall;
                    var tone = new Vector3(shade * (1 + 0.14f * warm), shade * (1 + 0.04f * warm), shade * (1 - 0.22f * warm));
                    var across = MathF.Sqrt(squared) / radius;
                    var bend = new Vector3(ex, ey, 0) / MathF.Max(MathF.Sqrt(squared), 1e-4f) * (0.55f * across) + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * (0.25f * fraction);
                    return new GrassSample(tone, bend, true);
                }
        }
        return soil;
    }
}
