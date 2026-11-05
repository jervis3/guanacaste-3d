using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Guanacaste3D.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var type = typeof(MainWindow).Assembly.GetType("Guanacaste3D.Desktop.Controls.MaterialBall3D", true)!;
        var ball = (Viewport3D)Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { .4, .2 }, null)!;
        var root = (Model3DGroup)((ModelVisual3D)ball.Children[0]).Content;
        var geometry = root.Children.OfType<GeometryModel3D>().Single();
        var mesh = (MeshGeometry3D)geometry.Geometry;
        if (mesh.Positions.Count < 500 || mesh.TriangleIndices.Count < 2000 || mesh.TextureCoordinates.Count != mesh.Positions.Count)
            throw new Exception("The material preview is not a textured 3D mesh");
        var bitmap = BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{20,40,60,255},4);
        type.GetMethod("SetTexture")!.Invoke(ball, [bitmap]);
        var diffuse = ((MaterialGroup)geometry.Material).Children.OfType<DiffuseMaterial>().Single();
        if (diffuse.Brush is not ImageBrush image || image.ImageSource != bitmap || geometry.Transform is not RotateTransform3D)
            throw new Exception("The preview does not apply the texture or expose a 3D rotation");
        Console.WriteLine($"PASS: {mesh.Positions.Count} textured vertices; material texture and 3D rotation.");
    }
}
