using ImageCutOut.Core;
using SkiaSharp;

namespace ImageCutOut.Core.Tests;

public sealed class ImageDocumentTests
{
    [Fact]
    public void CutUndoRedo_RestoresDimensionsAndDirtyState()
    {
        using var document = new ImageDocument(BitmapTestFactory.CreatePattern(100, 100), "test.png");

        Assert.False(document.IsDirty);
        Assert.True(document.TryCut(new CutSelection(CutDirection.Horizontal, 40, 60), out _));
        Assert.Equal((100, 80), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.True(document.TryCut(new CutSelection(CutDirection.Vertical, 25, 40), out _));
        Assert.Equal((85, 80), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.True(document.IsDirty);
        Assert.True(document.CanUndo);
        Assert.False(document.CanRedo);

        Assert.True(document.Undo());
        Assert.Equal((100, 80), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.True(document.CanUndo);
        Assert.True(document.CanRedo);
        Assert.True(document.IsDirty);

        Assert.True(document.Undo());
        Assert.Equal((100, 100), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.False(document.CanUndo);
        Assert.True(document.CanRedo);
        Assert.False(document.IsDirty);

        Assert.True(document.Redo());
        Assert.Equal((100, 80), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.True(document.Redo());
        Assert.Equal((85, 80), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.False(document.CanRedo);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void UndoBackToSavedVersion_ClearsDirtyMarker()
    {
        using var document = new ImageDocument(BitmapTestFactory.CreatePattern(100, 100), "test.png");
        Assert.True(document.TryCut(new CutSelection(CutDirection.Horizontal, 10, 20), out _));
        document.MarkSaved("saved.png");
        Assert.False(document.IsDirty);

        Assert.True(document.TryCut(new CutSelection(CutDirection.Vertical, 10, 20), out _));
        Assert.True(document.IsDirty);
        Assert.True(document.Undo());
        Assert.False(document.IsDirty);
        Assert.Equal("saved.png", document.FilePath);
    }

    [Fact]
    public void NewEditAfterUndo_ClearsRedo()
    {
        using var document = new ImageDocument(BitmapTestFactory.CreatePattern(100, 100), "test.png");
        Assert.True(document.TryCut(new CutSelection(CutDirection.Horizontal, 10, 20), out _));
        Assert.True(document.Undo());
        Assert.True(document.CanRedo);

        Assert.True(document.TryCut(new CutSelection(CutDirection.Vertical, 10, 20), out _));
        Assert.False(document.CanRedo);
    }

    [Fact]
    public void HistoryIsLimitedToTwentyChanges()
    {
        using var document = new ImageDocument(BitmapTestFactory.CreatePattern(100, 100), "test.png");
        for (int i = 0; i < 21; i++)
        {
            Assert.True(document.TryCut(new CutSelection(CutDirection.Vertical, 0, 2), out _));
        }

        for (int i = 0; i < 20; i++)
        {
            Assert.True(document.Undo());
        }

        Assert.False(document.Undo());
        Assert.Equal(98, document.CurrentBitmap.Width);
    }

    [Fact]
    public void InvalidCutDoesNotCreateHistory()
    {
        using var document = new ImageDocument(BitmapTestFactory.CreatePattern(100, 100), "test.png");
        Assert.False(document.TryCut(new CutSelection(CutDirection.Horizontal, 0, 100), out string? error));
        Assert.Equal("无法删除整张图片。", error);
        Assert.False(document.CanUndo);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void FullSizeWorkflow_PerformsThreeCutsAndStepwiseUndoRedo()
    {
        using var source = new SKBitmap(new SKImageInfo(
            3840,
            2160,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        source.Erase(new SKColor(25, 50, 75, 200));
        using var document = new ImageDocument(source, "large.jpg");

        Assert.True(document.TryCut(new CutSelection(CutDirection.Horizontal, 820, 1120), out _));
        Assert.Equal((3840, 1860), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.True(document.TryCut(new CutSelection(CutDirection.Vertical, 900, 1300), out _));
        Assert.Equal((3440, 1860), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.True(document.TryCut(new CutSelection(CutDirection.Horizontal, 100, 200), out _));
        Assert.Equal((3440, 1760), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));

        Assert.True(document.Undo());
        Assert.Equal((3440, 1860), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.True(document.Undo());
        Assert.Equal((3840, 1860), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
        Assert.True(document.Redo());
        Assert.Equal((3440, 1860), (document.CurrentBitmap.Width, document.CurrentBitmap.Height));
    }
}
