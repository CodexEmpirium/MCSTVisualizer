using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MCSTVisualizer.Services;

public static class ScreenshotExporter
{
    public static void SaveFrameworkElement(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);

        BitmapEncoder encoder = CreateEncoder(path);
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }

    private static BitmapEncoder CreateEncoder(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
    }
}
