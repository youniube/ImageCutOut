using SkiaSharp;

namespace ImageCutOut.Core;

public sealed class ImageDocument : IDisposable
{
    private readonly HistoryManager _history;
    private readonly CutOutOperation _cutOutOperation = new();
    private long _nextVersion;

    public ImageDocument(SKBitmap bitmap, string? filePath, int historyCapacity = 20)
    {
        CurrentBitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        FilePath = filePath;
        _history = new HistoryManager(historyCapacity);
    }

    public SKBitmap CurrentBitmap { get; private set; }

    public string? FilePath { get; private set; }

    public long DocumentVersion { get; private set; }

    public long SavedVersion { get; private set; }

    public bool IsDirty => DocumentVersion != SavedVersion;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public bool TryCut(CutSelection selection, out string? error)
    {
        int dimension = selection.Direction == CutDirection.Horizontal
            ? CurrentBitmap.Height
            : CurrentBitmap.Width;
        int start = Math.Clamp(selection.Start, 0, dimension);
        int end = Math.Clamp(selection.End, 0, dimension);
        int length = end - start;

        if (length < 2)
        {
            error = "选区太小，未执行裁切。";
            return false;
        }

        if (length >= dimension)
        {
            error = "无法删除整张图片。";
            return false;
        }

        SKBitmap? result = _cutOutOperation.Execute(CurrentBitmap, start, end, selection.Direction);
        if (result is null)
        {
            error = "裁切区域无效。";
            return false;
        }

        SKBitmap previous = CurrentBitmap;
        _history.RecordChange(previous, DocumentVersion);
        CurrentBitmap = result;
        DocumentVersion = ++_nextVersion;
        error = null;
        return true;
    }

    public bool Undo()
    {
        if (!_history.TryUndo(CurrentBitmap, DocumentVersion, out SKBitmap? restored, out long version))
        {
            return false;
        }

        CurrentBitmap = restored!;
        DocumentVersion = version;
        return true;
    }

    public bool Redo()
    {
        if (!_history.TryRedo(CurrentBitmap, DocumentVersion, out SKBitmap? restored, out long version))
        {
            return false;
        }

        CurrentBitmap = restored!;
        DocumentVersion = version;
        return true;
    }

    public void MarkSaved(string filePath)
    {
        FilePath = filePath;
        SavedVersion = DocumentVersion;
    }

    public void Dispose()
    {
        CurrentBitmap.Dispose();
        _history.Dispose();
    }
}
