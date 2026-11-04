using System.Numerics;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// The surface of water: a train of waves of different lengths running in different directions, summed as a height field
/// whose slope is worked out exactly (not by differences), so the sea stays smooth at a distance of kilometres where small
/// steps would drown in rounding. Ripples are the same waves, small and calm, for a pool; Waves are the open sea, with
/// white caps on the crests.
/// </summary>
internal static partial class SurfacePatterns
{
    /// <summary>Direction in degrees, length in metres at scale 1, steepness (height over length) and phase.</summary>
    private static readonly (float Direction, float Length, float Steepness, float Phase)[] WaveTrain =
    [
        (20f, 9.0f, 0.022f, 0.3f), (-38f, 3.7f, 0.020f, 1.7f), (68f, 1.6f, 0.016f, 4.1f),
        (108f, 0.72f, 0.012f, 2.2f), (-12f, 0.31f, 0.008f, 5.0f)
    ];

    private static readonly float[] WaveCos = WaveTrain.Select(wave => MathF.Cos(wave.Direction * MathF.PI / 180f)).ToArray();
    private static readonly float[] WaveSin = WaveTrain.Select(wave => MathF.Sin(wave.Direction * MathF.PI / 180f)).ToArray();
    private static readonly float WaveReach = WaveTrain.Sum(wave => wave.Steepness * wave.Length) * 1.45f;

    /// <summary>
    /// Height and slope of the water at a point of the plane. <paramref name="calm"/> scales every wave down (a pool is
    /// about a quarter of the sea); <paramref name="fade"/> in 0–1 takes the two shortest waves away at a distance,
    /// where they would only sparkle.
    /// </summary>
    public static (float Height, float SlopeU, float SlopeV) WaterSurface(float u, float v, float scale, float calm, float fade)
    {
        float height = 0, du = 0, dv = 0;
        for (var index = 0; index < WaveTrain.Length; index++)
        {
            var wave = WaveTrain[index];
            var length = wave.Length * scale;
            // Real waves are neither equally tall nor evenly spaced: a slow noise swells and eases each train and bends its
            // crests a little, which breaks the regular grid a handful of plain sines would show from above. Their slope
            // is left out of the derivative below, as it is a small fraction of the wave's own.
            var envelope = 0.55f + 0.9f * ValueNoise(u / (6f * length) + index * 17f, v / (6f * length) - index * 5f);
            var bend = 3.0f * (ValueNoise(u / (2.5f * length) + index * 3f, v / (2.5f * length) + 9f) - 0.5f);
            var amplitude = wave.Steepness * length * calm * envelope * (index >= 3 ? fade : 1f);
            if (amplitude <= 0) continue;
            var k = 2 * MathF.PI / length;
            var phase = k * (WaveCos[index] * u + WaveSin[index] * v) + wave.Phase + bend;
            height += amplitude * MathF.Sin(phase);
            var slope = amplitude * k * MathF.Cos(phase);
            du += slope * WaveCos[index];
            dv += slope * WaveSin[index];
        }
        return (height, du, dv);
    }

    /// <summary>Tilts a normal by the waves at its point; the sea fades its small waves much farther away than a pool does.</summary>
    private static Vector3 WaterRelief(SurfacePattern pattern, float scale, Vector3 position, Vector3 normal, float distance)
    {
        var (u, v) = PlaneCoordinates(position, normal);
        var open = pattern == SurfacePattern.Waves;
        var fade = 1 / (1 + distance * distance / (open ? 90000f : 900f));
        var (_, du, dv) = WaterSurface(u, v, scale, open ? 1f : 0.25f, fade);
        return Bend(normal, du, dv);
    }

    /// <summary>
    /// The colour of the water given its height: a little lighter on the crests, and on the open sea white caps where the
    /// crest is highest, broken up by foam patches so they never read as a clean stripe.
    /// </summary>
    private static Vector3 WaterTone(SurfacePattern pattern, float scale, float u, float v)
    {
        var open = pattern == SurfacePattern.Waves;
        var (height, _, _) = WaterSurface(u * scale, v * scale, scale, open ? 1f : 0.25f, 1f);
        var crest = Math.Clamp(height / (WaveReach * (open ? 1f : 0.25f)) * 0.5f + 0.5f, 0f, 1f);
        var tone = 0.9f + 0.2f * crest;
        if (!open) return new Vector3(tone);
        var foam = SmoothStep(0.80f, 0.97f, crest) * SmoothStep(0.35f, 0.7f, ValueNoise(u * scale * 2.7f + 9, v * scale * 2.7f - 4));
        return new Vector3(tone + 7f * foam);
    }
}
