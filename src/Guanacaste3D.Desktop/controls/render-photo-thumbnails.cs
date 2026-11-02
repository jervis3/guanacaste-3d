using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>Decodes and remembers the small previews saved with each photo.</summary>
internal static class PhotoThumbnails
{
    private static readonly Dictionary<Guid, (int Length, ImageSource Image)> Cache = [];

    /// <summary>The photo's preview, or null when none was captured or the stored data is not a picture.</summary>
    public static ImageSource? Of(RenderSceneBookmark photo)
    {
        if (string.IsNullOrEmpty(photo.Thumbnail)) return null;
        if (Cache.TryGetValue(photo.Id, out var cached) && cached.Length == photo.Thumbnail.Length) return cached.Image;
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(photo.Thumbnail));
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            Cache[photo.Id] = (photo.Thumbnail.Length, image);
            return image;
        }
        catch (Exception error) when (error is FormatException or NotSupportedException or IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
