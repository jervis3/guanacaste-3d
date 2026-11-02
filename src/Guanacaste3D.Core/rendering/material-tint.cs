using System.Globalization;
using System.Numerics;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// Lumion's "Colorización": a colour that multiplies a material's base colour, so a brick texture can be made
/// redder or a white render warmer without replacing the image. Stored in the project as an sRGB <c>#RRGGBB</c> text.
/// </summary>
public static class MaterialTint
{
    private const float Gamma = 2.2f;

    /// <summary>Reads <c>#RRGGBB</c> into a linear colour; false for anything else, so a bad file never tints.</summary>
    public static bool TryParse(string? hex, out Vector3 linear)
    {
        linear = Vector3.One;
        if (hex is not { Length: 7 } || hex[0] != '#' ||
            !int.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            return false;
        float Channel(int shift) => MathF.Pow(((rgb >> shift) & 0xFF) / 255f, Gamma);
        linear = new Vector3(Channel(16), Channel(8), Channel(0));
        return true;
    }

    /// <summary>Whether the text is absent (no tint) or a well-formed colour.</summary>
    public static bool IsValid(string? hex) => hex is null || TryParse(hex, out _);

    /// <summary>The material with its base colour multiplied by the tint; unchanged when there is none.</summary>
    public static RenderMaterial Apply(RenderMaterial material, string? hex) =>
        TryParse(hex, out var tint) ? material with { BaseColor = material.BaseColor * tint } : material;
}
