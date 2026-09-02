using ImageCutOut.Core;
using SkiaSharp;

namespace ImageCutOut.Core.Tests;

public sealed class CutOutOperationTests
{
    private readonly CutOutOperation _operation = new();

    [Fact]
    public void HorizontalCut_CopiesTopAndBottomWithoutASeam()
    {
        using SKBitmap source = BitmapTestFactory.CreatePattern(100, 100);
        using SKBitmap? result = _operation.Execute(source, 40, 60, CutDirection.Horizontal);

        Assert.NotNull(result);
        Assert.Equal(100, result.Width);
        Assert.Equal(80, result.Height);

        for (int y = 0; y < result.Height; y++)
        {
            int expectedSourceY = y < 40 ? y : y + 20;
            for (int x = 0; x < result.Width; x++)
            {
                Assert.Equal(source.GetPixel(x, expectedSourceY), result.GetPixel(x, y));
            }
        }

        Assert.Equal(source.GetPixel(50, 39), result.GetPixel(50, 39));
        Assert.Equal(source.GetPixel(50, 60), result.GetPixel(50, 40));
    }

    [Fact]
    public void VerticalCut_CopiesLeftAndRightWithoutASeam()
    {
        using SKBitmap source = BitmapTestFactory.CreatePattern(100, 100);
        using SKBitmap? result = _operation.Execute(source, 25, 40, CutDirection.Vertical);

        Assert.NotNull(result);
        Assert.Equal(85, result.Width);
        Assert.Equal(100, result.Height);

        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                int expectedSourceX = x < 25 ? x : x + 15;
                Assert.Equal(source.GetPixel(expectedSourceX, y), result.GetPixel(x, y));
            }
        }

        Assert.Equal(source.GetPixel(24, 50), result.GetPixel(24, 50));
        Assert.Equal(source.GetPixel(40, 50), result.GetPixel(25, 50));
    }

    [Theory]
    [InlineData(CutDirection.Horizontal)]
    [InlineData(CutDirection.Vertical)]
    public void ReversedCoordinates_AreEquivalent(CutDirection direction)
    {
        using SKBitmap source = BitmapTestFactory.CreatePattern(100, 100);
        using SKBitmap? forward = _operation.Execute(source, 40, 60, direction);
        using SKBitmap? reverse = _operation.Execute(source, 60, 40, direction);

        Assert.NotNull(forward);
        Assert.NotNull(reverse);
        Assert.Equal(forward.Width, reverse.Width);
        Assert.Equal(forward.Height, reverse.Height);

        for (int y = 0; y < forward.Height; y++)
        {
            for (int x = 0; x < forward.Width; x++)
            {
                Assert.Equal(forward.GetPixel(x, y), reverse.GetPixel(x, y));
            }
        }
    }

    [Theory]
    [InlineData(50, 50)]
    [InlineData(50, 51)]
    public void TooSmallRegion_ReturnsNullAndLeavesSourceUntouched(int start, int end)
    {
        using SKBitmap source = BitmapTestFactory.CreatePattern(100, 100);
        SKColor before = source.GetPixel(50, 50);

        using SKBitmap? result = _operation.Execute(source, start, end, CutDirection.Horizontal);

        Assert.Null(result);
        Assert.Equal(100, source.Width);
        Assert.Equal(100, source.Height);
        Assert.Equal(before, source.GetPixel(50, 50));
    }

    [Theory]
    [InlineData(CutDirection.Horizontal)]
    [InlineData(CutDirection.Vertical)]
    public void WholeImageCut_IsRejected(CutDirection direction)
    {
        using SKBitmap source = BitmapTestFactory.CreatePattern(100, 100);
        using SKBitmap? result = _operation.Execute(source, -50, 150, direction);
        Assert.Null(result);
    }

    [Fact]
    public void PngStyleAlphaPixels_AreCopiedExactly()
    {
        using SKBitmap source = BitmapTestFactory.CreatePattern(12, 12);
        using SKBitmap? result = _operation.Execute(source, 4, 7, CutDirection.Horizontal);

        Assert.NotNull(result);
        for (int x = 0; x < source.Width; x++)
        {
            Assert.Equal(source.GetPixel(x, 3), result.GetPixel(x, 3));
            Assert.Equal(source.GetPixel(x, 7), result.GetPixel(x, 4));
        }
    }

    [Fact]
    public void TenThousandSquareImage_CutsWithoutResampling()
    {
        using var source = new SKBitmap(new SKImageInfo(
            10_000,
            10_000,
            SKColorType.Gray8,
            SKAlphaType.Opaque));
        source.Erase(new SKColor(73, 73, 73));

        using SKBitmap? result = _operation.Execute(source, 4_900, 5_100, CutDirection.Horizontal);

        Assert.NotNull(result);
        Assert.Equal(10_000, result.Width);
        Assert.Equal(9_800, result.Height);
        Assert.Equal(source.GetPixel(5_000, 5_100), result.GetPixel(5_000, 4_900));
    }
}
