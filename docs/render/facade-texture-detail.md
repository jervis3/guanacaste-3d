# Facade texture detail

The image shows the jacaranda trunk and exposed concrete in a reconstructed
breeze-block house scene. It was rendered using the shipped families at 800 ×
600 pixels with 96 passes.

![Close view of the tree and facade](facade-texture-detail.png)

The jacaranda trunk uses 2048 × 2048 color and normal maps. Cast concrete uses
a procedural finish, while white painted surfaces use a separate material.

```powershell
$env:G3_SHOT_ONLY = 'cobogo-facade-detail'
dotnet run -c Release --project tests/Guanacaste3D.RenderBench -- --shot --out artifacts/facade-detail --width 800 --height 600 --passes 96
```
