namespace Guanacaste3D.Core.Rendering;

/// <summary>A named camera and lighting setup stored independently in the project.</summary>
public sealed record RenderSceneBookmark(Guid Id, string Name, RenderSetup Setup);
