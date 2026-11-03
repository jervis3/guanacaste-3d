namespace Guanacaste3D.Core;

public sealed partial class DocumentSession
{
    /// <summary>Smallest clear distance, in meters, worth a dimension: anything closer touches or overlaps.</summary>
    public const double MinimumMeasurableGap = 0.001;

    /// <summary>
    /// Adds the dimension of the clear distance between two elements of one level: walls, openings, columns, rooms,
    /// floors, structural elements, or a grid axis as the second one. The direction is the one in which they are
    /// farthest apart. The dimension follows both elements when either moves, like every other one.
    /// </summary>
    public Guid AddDimensionBetween(Guid ownerId, Guid referenceId)
    {
        if (ownerId == referenceId) throw new InvalidDataException("Two different elements are needed.");
        if (PlanDimensions.Bounds(Document, ownerId) is not { } owner) throw new InvalidDataException("The element does not exist.");
        PlanBounds target;
        if (Document.Grids.FirstOrDefault(axis => axis.Id == referenceId) is { } grid)
            target = new PlanBounds(Math.Min(grid.Start.X, grid.End.X), Math.Min(grid.Start.Y, grid.End.Y),
                Math.Max(grid.Start.X, grid.End.X), Math.Max(grid.Start.Y, grid.End.Y));
        else if (PlanDimensions.Bounds(Document, referenceId) is { } reference)
        {
            if (reference.LevelId != owner.LevelId) throw new InvalidDataException("The elements are on different levels.");
            target = reference.Bounds;
        }
        else throw new InvalidDataException("The element does not exist.");

        var gaps = new (PlanDimensionKind Kind, double Gap)[]
        {
            (PlanDimensionKind.Left, owner.Bounds.MinX - target.MaxX),
            (PlanDimensionKind.Right, target.MinX - owner.Bounds.MaxX),
            (PlanDimensionKind.Below, owner.Bounds.MinY - target.MaxY),
            (PlanDimensionKind.Above, target.MinY - owner.Bounds.MaxY)
        };
        var best = gaps.MaxBy(candidate => candidate.Gap);
        if (!(best.Gap > MinimumMeasurableGap)) throw new InvalidDataException("The elements touch or overlap, so there is no distance to measure.");
        return AddDimension(new PlanDimension(Guid.Empty, owner.LevelId, ownerId, referenceId, best.Kind));
    }
}
