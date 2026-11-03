using System.Globalization;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>
/// Geometric rough-opening returns through each constant-thickness wall layer.
/// These exposed cross-sections are not a specification for plaster wrapping, paint,
/// frames, trim or finished jambs; do not add them to finish takeoffs automatically.
/// </summary>
public sealed record WallOpeningRevealQuantity(
    Guid WallId, Guid OpeningId, OpeningKind OpeningKind, string Layer, double Thickness,
    double JambArea, double HeadArea, double SillArea)
{
    public double TotalArea => JambArea + HeadArea + SillArea;
}

/// <summary>Deterministic geometric takeoff of jambs, heads and window sills by wall layer.</summary>
public static class WallOpeningRevealSchedule
{
    /// <summary>
    /// Computes two vertical jambs and one head per opening. A sill return exists
    /// only when solid wall remains below the opening; a ground-level opening has none.
    /// The same opening validation and layer thicknesses as the wall BIM takeoff apply.
    /// </summary>
    public static IReadOnlyList<WallOpeningRevealQuantity> For(
        Wall wall, WallType type, IEnumerable<WallOpening> openings)
    {
        ArgumentNullException.ThrowIfNull(wall);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(openings);

        var hosted = openings.ToArray();
        var layers = WallLayerQuantitySchedule.For(wall, type, hosted);
        return hosted.OrderBy(opening => opening.Id)
            .SelectMany(opening => layers.Select(layer => new WallOpeningRevealQuantity(
                wall.Id, opening.Id, opening.Kind, layer.Layer, layer.Thickness,
                2 * opening.Height * layer.Thickness,
                opening.Width * layer.Thickness,
                opening.Sill > 0 ? opening.Width * layer.Thickness : 0)))
            .ToArray();
    }

    /// <summary>
    /// One CSV row per opening and nonzero layer. Values are meters and square meters;
    /// opening returns are geometric potential, not quantities of installed finishes.
    /// </summary>
    public static string ToCsv(BuildingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        var types = document.WallTypes.ToDictionary(type => type.Id);
        var levels = document.Levels.ToDictionary(level => level.Id);
        var openings = document.Openings.ToLookup(opening => opening.WallId);
        var csv = new StringBuilder(
            "Level,WallId,OpeningId,OpeningKind,Layer,Thickness_m,JambArea_m2,HeadArea_m2,SillArea_m2,TotalRevealArea_m2\n");

        foreach (var wall in document.Walls.OrderBy(wall => wall.LevelId).ThenBy(wall => wall.Id))
        {
            foreach (var reveal in For(wall, types[wall.WallTypeId], openings[wall.Id]))
            {
                csv.Append(SpreadsheetCsv.Quote(levels[wall.LevelId].Name)).Append(',')
                    .Append(reveal.WallId).Append(',')
                    .Append(reveal.OpeningId).Append(',')
                    .Append(reveal.OpeningKind).Append(',')
                    .Append(reveal.Layer).Append(',')
                    .Append(Format(reveal.Thickness)).Append(',')
                    .Append(Format(reveal.JambArea)).Append(',')
                    .Append(Format(reveal.HeadArea)).Append(',')
                    .Append(Format(reveal.SillArea)).Append(',')
                    .Append(Format(reveal.TotalArea)).AppendLine();
            }
        }
        return csv.ToString();
    }

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
