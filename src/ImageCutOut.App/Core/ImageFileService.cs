using SkiaSharp;

namespace ImageCutOut.Core;

public sealed class ImageFileService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp"
    };

    public bool IsSupportedPath(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    public SKBitmap Load(string path)
    {
        if (!IsSupportedPath(path))
        {
            throw new NotSupportedException("仅支持 PNG、JPG、JPEG、WebP 和 BMP 图片。 ");
        }

        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using SKCodec codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException("无法识别或读取该图片。 ");
        SKImageInfo codecInfo = codec.Info;
        var decodeInfo = new SKImageInfo(
            codecInfo.Width,
            codecInfo.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            codecInfo.ColorSpace);
        var decoded = new SKBitmap(decodeInfo);

        SKCodecResult result = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (result != SKCodecResult.Success)
        {
            decoded.Dispose();
            throw new InvalidDataException($"图片解码失败：{result}。");
        }

        if (codec.EncodedOrigin == SKEncodedOrigin.TopLeft)
        {
            return decoded;
        }

        SKBitmap oriented = ApplyOrientation(decoded, codec.EncodedOrigin);
        decoded.Dispose();
        return oriented;
    }

    public void Save(SKBitmap bitmap, string path)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        SKEncodedImageFormat format = GetFormat(path);
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("保存目录不存在。 ");
        }

        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var output = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough))
            {
                if (format == SKEncodedImageFormat.Bmp)
                {
                    WriteBmp(bitmap, output);
                }
                else
                {
                    using SKImage image = SKImage.FromBitmap(bitmap);
                    using SKData data = image.Encode(format, format == SKEncodedImageFormat.Jpeg ? 95 : 100)
                        ?? throw new InvalidOperationException("图片编码失败。 ");
                    data.SaveTo(output);
                }

                output.Flush(flushToDisk: true);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static SKEncodedImageFormat GetFormat(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            ".bmp" => SKEncodedImageFormat.Bmp,
            _ => throw new NotSupportedException("保存格式必须是 PNG、JPG、JPEG、WebP 或 BMP。 ")
        };

    private static unsafe void WriteBmp(SKBitmap bitmap, Stream output)
    {
        if (bitmap.ColorType != SKColorType.Bgra8888 || bitmap.AlphaType != SKAlphaType.Premul)
        {
            throw new InvalidOperationException("BMP 保存要求 BGRA8888 Premul 图片。 ");
        }

        int pixelBytesPerRow = checked(bitmap.Width * 3);
        int fileRowBytes = checked((pixelBytesPerRow + 3) & ~3);
        int pixelDataSize = checked(fileRowBytes * bitmap.Height);
        const int pixelDataOffset = 14 + 40;
        int fileSize = checked(pixelDataOffset + pixelDataSize);

        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(pixelDataOffset);

        writer.Write(40);
        writer.Write(bitmap.Width);
        writer.Write(-bitmap.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)24);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(3_780);
        writer.Write(3_780);
        writer.Write(0);
        writer.Write(0);

        byte[] row = GC.AllocateUninitializedArray<byte>(fileRowBytes);
        byte* sourceBase = (byte*)bitmap.GetPixels();
        for (int y = 0; y < bitmap.Height; y++)
        {
            byte* sourceRow = sourceBase + y * bitmap.RowBytes;
            int destinationOffset = 0;
            for (int x = 0; x < bitmap.Width; x++)
            {
                byte blue = sourceRow[x * 4];
                byte green = sourceRow[x * 4 + 1];
                byte red = sourceRow[x * 4 + 2];
                byte alpha = sourceRow[x * 4 + 3];
                row[destinationOffset++] = Unpremultiply(blue, alpha);
                row[destinationOffset++] = Unpremultiply(green, alpha);
                row[destinationOffset++] = Unpremultiply(red, alpha);
            }

            row.AsSpan(pixelBytesPerRow).Clear();
            writer.Write(row);
        }
    }

    private static byte Unpremultiply(byte value, byte alpha)
    {
        if (alpha == 0)
        {
            return 0;
        }

        return (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
    }

    private static unsafe SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        bool swapsDimensions = origin is
            SKEncodedOrigin.LeftTop or
            SKEncodedOrigin.RightTop or
            SKEncodedOrigin.RightBottom or
            SKEncodedOrigin.LeftBottom;
        int destinationWidth = swapsDimensions ? source.Height : source.Width;
        int destinationHeight = swapsDimensions ? source.Width : source.Height;
        var info = new SKImageInfo(
            destinationWidth,
            destinationHeight,
            source.ColorType,
            source.AlphaType,
            source.ColorSpace);
        var destination = new SKBitmap(info);
        uint* sourcePixels = (uint*)source.GetPixels();
        uint* destinationPixels = (uint*)destination.GetPixels();
        int sourceStride = source.RowBytes / sizeof(uint);
        int destinationStride = destination.RowBytes / sizeof(uint);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                (int destinationX, int destinationY) = origin switch
                {
                    SKEncodedOrigin.TopRight => (source.Width - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (source.Width - 1 - x, source.Height - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, source.Height - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (source.Height - 1 - y, x),
                    SKEncodedOrigin.RightBottom => (source.Height - 1 - y, source.Width - 1 - x),
                    SKEncodedOrigin.LeftBottom => (y, source.Width - 1 - x),
                    _ => (x, y)
                };
                destinationPixels[destinationY * destinationStride + destinationX] =
                    sourcePixels[y * sourceStride + x];
            }
        }

        return destination;
    }
}
