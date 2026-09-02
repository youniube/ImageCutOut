namespace ImageCutOut.Core;

public static class ImageCoordinateMapper
{
    public static (double X, double Y) ScreenToImage(
        double screenX,
        double screenY,
        double imageOffsetX,
        double imageOffsetY,
        double displayScale,
        int imageWidth,
        int imageHeight)
    {
        if (displayScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayScale));
        }

        double imageX = Math.Clamp((screenX - imageOffsetX) / displayScale, 0, imageWidth);
        double imageY = Math.Clamp((screenY - imageOffsetY) / displayScale, 0, imageHeight);
        return (imageX, imageY);
    }
}
