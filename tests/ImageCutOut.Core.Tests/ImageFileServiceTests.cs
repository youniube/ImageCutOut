using ImageCutOut.Core;
using SkiaSharp;

namespace ImageCutOut.Core.Tests;

public sealed class ImageFileServiceTests
{
    private readonly ImageFileService _service = new();

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".webp")]
    [InlineData(".bmp")]
    public void SaveThenLoad_SupportsRequiredFormats(string extension)
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "round-trip" + extension);
        using SKBitmap source = BitmapTestFactory.CreatePattern(32, 24);

        _service.Save(source, path);
        using SKBitmap loaded = _service.Load(path);

        Assert.Equal(32, loaded.Width);
        Assert.Equal(24, loaded.Height);
        Assert.Equal(SKColorType.Bgra8888, loaded.ColorType);
        Assert.Equal(SKAlphaType.Premul, loaded.AlphaType);
    }

    [Fact]
    public void PngRoundTrip_PreservesAlpha()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "alpha.png");
        using SKBitmap source = BitmapTestFactory.CreatePattern(16, 16);

        _service.Save(source, path);
        using SKBitmap loaded = _service.Load(path);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Assert.Equal(source.GetPixel(x, y), loaded.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void Overwrite_ReplacesCompletedFileAndLeavesNoTemporaryFile()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "overwrite.png");
        using SKBitmap first = BitmapTestFactory.CreatePattern(20, 20);
        using SKBitmap second = BitmapTestFactory.CreatePattern(12, 8);

        _service.Save(first, path);
        _service.Save(second, path);
        using SKBitmap loaded = _service.Load(path);

        Assert.Equal((12, 8), (loaded.Width, loaded.Height));
        Assert.Single(Directory.EnumerateFiles(temp.Path));
    }

    [Fact]
    public void ExifOrientationSix_RotatesPixelsToTheirDisplayOrientation()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "oriented.jpg");
        using SKBitmap source = BitmapTestFactory.CreatePattern(20, 30);
        using SKImage image = SKImage.FromBitmap(source);
        using SKData encoded = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        byte[] jpeg = encoded.ToArray();
        File.WriteAllBytes(path, AddExifOrientation(jpeg, 6));

        using SKBitmap loaded = _service.Load(path);

        Assert.Equal(30, loaded.Width);
        Assert.Equal(20, loaded.Height);
    }

    [Fact]
    public void UnsupportedExtension_IsRejected()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "image.gif");
        File.WriteAllBytes(path, [0x47, 0x49, 0x46]);

        Assert.Throws<NotSupportedException>(() => _service.Load(path));
        using SKBitmap source = BitmapTestFactory.CreatePattern(2, 2);
        Assert.Throws<NotSupportedException>(() => _service.Save(source, path));
    }

    [Fact]
    public void JpegOverwrite_ReopensWithTheEditedDimensions()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "edited.jpg");
        using SKBitmap source = BitmapTestFactory.CreatePattern(100, 100);
        using SKBitmap? edited = new CutOutOperation().Execute(source, 25, 40, CutDirection.Vertical);
        Assert.NotNull(edited);

        _service.Save(source, path);
        _service.Save(edited, path);
        using SKBitmap reopened = _service.Load(path);

        Assert.Equal((85, 100), (reopened.Width, reopened.Height));
    }

    private static byte[] AddExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] payload =
        [
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01,
            0x03, 0x00,
            0x01, 0x00, 0x00, 0x00,
            (byte)orientation, (byte)(orientation >> 8), 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
        int segmentLength = payload.Length + 2;
        byte[] result = new byte[jpeg.Length + payload.Length + 4];
        result[0] = 0xFF;
        result[1] = 0xD8;
        result[2] = 0xFF;
        result[3] = 0xE1;
        result[4] = (byte)(segmentLength >> 8);
        result[5] = (byte)segmentLength;
        payload.CopyTo(result, 6);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(6 + payload.Length));
        return result;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ImageCutOut.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
