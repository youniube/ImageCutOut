using ImageCutOut.Core;

namespace ImageCutOut.Core.Tests;

public sealed class ImageCoordinateMapperTests
{
    [Fact]
    public void QuarterScale_MapsScreenPixelsToOriginalPixels()
    {
        (double _, double startY) = ImageCoordinateMapper.ScreenToImage(
            0, 200, 0, 0, 0.25, 3840, 2160);
        (double _, double endY) = ImageCoordinateMapper.ScreenToImage(
            0, 300, 0, 0, 0.25, 3840, 2160);

        Assert.Equal(800, startY);
        Assert.Equal(1200, endY);
    }

    [Fact]
    public void OffsetAndOutOfBoundsCoordinates_AreMappedAndClamped()
    {
        (double x, double y) = ImageCoordinateMapper.ScreenToImage(
            25, 900, 100, 50, 0.5, 1000, 800);

        Assert.Equal(0, x);
        Assert.Equal(800, y);
    }

    [Fact]
    public void InvalidScale_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ImageCoordinateMapper.ScreenToImage(0, 0, 0, 0, 0, 100, 100));
    }
}
