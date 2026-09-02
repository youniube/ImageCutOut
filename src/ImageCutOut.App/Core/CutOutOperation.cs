using SkiaSharp;

namespace ImageCutOut.Core;

public sealed class CutOutOperation
{
    public SKBitmap? Execute(SKBitmap source, int start, int end, CutDirection direction)
    {
        ArgumentNullException.ThrowIfNull(source);

        int dimension = direction == CutDirection.Horizontal ? source.Height : source.Width;
        int cutStart = Math.Clamp(Math.Min(start, end), 0, dimension);
        int cutEnd = Math.Clamp(Math.Max(start, end), 0, dimension);
        int cutLength = cutEnd - cutStart;

        if (cutLength < 2 || cutLength >= dimension || source.BytesPerPixel <= 0)
        {
            return null;
        }

        return direction == CutDirection.Horizontal
            ? ExecuteHorizontal(source, cutStart, cutEnd)
            : ExecuteVertical(source, cutStart, cutEnd);
    }

    private static unsafe SKBitmap ExecuteHorizontal(SKBitmap source, int top, int bottom)
    {
        int newHeight = source.Height - (bottom - top);
        var destination = CreateCompatibleBitmap(source, source.Width, newHeight);
        int bytesToCopy = checked(source.Width * source.BytesPerPixel);
        byte* sourceBase = (byte*)source.GetPixels();
        byte* destinationBase = (byte*)destination.GetPixels();

        for (int destinationY = 0; destinationY < newHeight; destinationY++)
        {
            int sourceY = destinationY < top ? destinationY : destinationY + (bottom - top);
            Buffer.MemoryCopy(
                sourceBase + sourceY * source.RowBytes,
                destinationBase + destinationY * destination.RowBytes,
                destination.RowBytes,
                bytesToCopy);
        }

        return destination;
    }

    private static unsafe SKBitmap ExecuteVertical(SKBitmap source, int left, int right)
    {
        int cutWidth = right - left;
        int newWidth = source.Width - cutWidth;
        var destination = CreateCompatibleBitmap(source, newWidth, source.Height);
        int bytesPerPixel = source.BytesPerPixel;
        int leftBytes = checked(left * bytesPerPixel);
        int rightBytes = checked((source.Width - right) * bytesPerPixel);
        byte* sourceBase = (byte*)source.GetPixels();
        byte* destinationBase = (byte*)destination.GetPixels();

        for (int y = 0; y < source.Height; y++)
        {
            byte* sourceRow = sourceBase + y * source.RowBytes;
            byte* destinationRow = destinationBase + y * destination.RowBytes;

            if (leftBytes > 0)
            {
                Buffer.MemoryCopy(sourceRow, destinationRow, destination.RowBytes, leftBytes);
            }

            if (rightBytes > 0)
            {
                Buffer.MemoryCopy(
                    sourceRow + right * bytesPerPixel,
                    destinationRow + leftBytes,
                    destination.RowBytes - leftBytes,
                    rightBytes);
            }
        }

        return destination;
    }

    private static SKBitmap CreateCompatibleBitmap(SKBitmap source, int width, int height)
    {
        var info = new SKImageInfo(
            width,
            height,
            source.ColorType,
            source.AlphaType,
            source.ColorSpace);
        return new SKBitmap(info);
    }
}
