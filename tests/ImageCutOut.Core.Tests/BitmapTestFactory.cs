using SkiaSharp;

namespace ImageCutOut.Core.Tests;

internal static class BitmapTestFactory
{
    public static SKBitmap CreatePattern(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor(
                    (byte)(x % 251),
                    (byte)(y % 241),
                    (byte)((x * 3 + y * 5) % 239),
                    (byte)(64 + (x + y) % 192)));
            }
        }

        return bitmap;
    }
}
