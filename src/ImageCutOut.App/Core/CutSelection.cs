namespace ImageCutOut.Core;

public readonly record struct CutSelection(CutDirection Direction, int StartPixel, int EndPixel)
{
    public int Start => Math.Min(StartPixel, EndPixel);

    public int End => Math.Max(StartPixel, EndPixel);

    public int Length => End - Start;
}
