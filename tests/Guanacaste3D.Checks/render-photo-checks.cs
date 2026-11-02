using System.Numerics;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;

/// <summary>Photos (saved camera + style + filters + preview) and the Lumion-style travel speeds.</summary>
static class RenderPhotoChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(ArchitecturalLibrary.CreateProject("Fotos"));
        var raised = 0;
        var undoDepth = session.UndoNames.Count;
        session.RenderPhotosChanged += (_, _) => raised++;
        var changedModel = 0;
        session.Changed += (_, _) => changedModel++;

        var first = new RenderSetup { CameraX = 3, CameraY = -4, CameraZ = 1.6, YawDegrees = 120, PitchDegrees = -5, StyleId = "none" };
        var id = session.AddRenderPhoto("Fachada", first, "AAAA");
        check(session.Document.RenderScenes.Count == 1 && session.Document.RenderScenes[0].Thumbnail == "AAAA",
            "A photo keeps its camera setup and preview");
        check(raised == 1 && changedModel == 0 && session.UndoNames.Count == undoDepth,
            "Adding a photo does not rebuild the model nor add an undo step");

        var warm = first with { LutPresetId = ColorLutPresets.All.First(p => p.Id != "none").Id, LutStrength = 0.9 };
        session.UpdateRenderPhoto(id, warm, thumbnail: "BBBB");
        var stored = session.Document.RenderScenes[0];
        check(stored.Setup.LutStrength == 0.9 && stored.Thumbnail == "BBBB" && stored.Name == "Fachada",
            "Editing a photo's filters replaces its setup and preview but keeps its name");
        session.UpdateRenderPhoto(id, name: "Fachada norte");
        check(session.Document.RenderScenes[0].Name == "Fachada norte" && session.Document.RenderScenes[0].Thumbnail == "BBBB",
            "Renaming a photo keeps its preview");

        session.AddRenderPhoto("Interior", first with { CameraX = 0 }, null, index: 0);
        check(session.Document.RenderScenes[0].Name == "Interior" && session.Document.RenderScenes[1].Id == id,
            "A duplicated photo can be inserted next to its source");

        var path = Path.Combine(Path.GetTempPath(), "g3d-photos-" + Guid.NewGuid().ToString("N") + ".g3d");
        try
        {
            ProjectFile.Save(path, session.Document);
            var loaded = ProjectFile.Load(path).RenderScenes;
            check(loaded.Count == 2 && loaded[1].Thumbnail == "BBBB" && loaded[1].Setup.LutStrength == 0.9 && loaded[0].Thumbnail is null,
                "Photos with their previews survive a .g3d round trip");
        }
        finally { if (File.Exists(path)) File.Delete(path); }

        var rejected = false;
        try { session.UpdateRenderPhoto(id, thumbnail: new string('A', RenderSceneBookmark.MaximumThumbnailLength + 1)); }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "An oversized preview is rejected before it can bloat the project");

        session.DeleteRenderPhoto(id);
        check(session.Document.RenderScenes.Count == 1 && session.Document.RenderScenes[0].Name == "Interior",
            "Deleting a photo removes only that photo");

        var camera = new NavigationCamera { Position = Vector3.Zero, YawDegrees = 0, PitchDegrees = 0 };
        var normal = CameraTravel.Displacement(camera, new Vector3(1, 0, 0), 2, 0.1f, false, 1f).Length();
        var fast = CameraTravel.Displacement(camera, new Vector3(1, 0, 0), 2, 0.1f, false, 4f).Length();
        var slow = CameraTravel.Displacement(camera, new Vector3(1, 0, 0), 2, 0.1f, false, 0.25f).Length();
        check(Math.Abs(fast / normal - 4) < 0.01 && Math.Abs(slow / normal - 0.25) < 0.01,
            "Shift flies four times faster and Space a quarter as fast, as in Lumion");
        check(CameraTravel.Displacement(camera, new Vector3(1, 0, 0), 2, 0.1f, false, float.NaN) == Vector3.Zero,
            "An invalid speed multiplier cannot move the camera");
    }
}
