using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Core;

public sealed partial class DocumentSession
{
    /// <summary>Adds a named render setup as an undoable project change.</summary>
    public Guid AddRenderScene(string name, RenderSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        setup.Validate();
        name = name.Trim();
        if (name.Length is < 1 or > 120) throw new InvalidDataException("The render scene name is invalid.");
        var scene = new RenderSceneBookmark(Guid.NewGuid(), name, setup);
        Commit("Transaction.AddRenderScene", document => document.RenderScenes.Add(scene));
        return scene.Id;
    }

    /// <summary>Removes a saved render scene in one undoable transaction.</summary>
    public void RemoveRenderScene(Guid id)
    {
        if (!Document.RenderScenes.Any(scene => scene.Id == id))
            throw new InvalidDataException("The render scene does not exist.");
        Commit("Transaction.RemoveRenderScene", document => document.RenderScenes.RemoveAll(scene => scene.Id == id));
    }
}
