using SkiaSharp;

namespace ImageCutOut.Core;

public sealed class HistoryManager : IDisposable
{
    private readonly LinkedList<ImageState> _undo = [];
    private readonly Stack<ImageState> _redo = [];

    public HistoryManager(int capacity = 20)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
    }

    public int Capacity { get; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoCount => _undo.Count;

    public void RecordChange(SKBitmap previousBitmap, long previousVersion)
    {
        ArgumentNullException.ThrowIfNull(previousBitmap);
        ClearStack(_redo);
        AddUndo(new ImageState(previousBitmap, previousVersion));
    }

    public bool TryUndo(
        SKBitmap currentBitmap,
        long currentVersion,
        out SKBitmap? restoredBitmap,
        out long restoredVersion)
    {
        ArgumentNullException.ThrowIfNull(currentBitmap);

        if (_undo.Last is null)
        {
            restoredBitmap = null;
            restoredVersion = currentVersion;
            return false;
        }

        _redo.Push(new ImageState(currentBitmap, currentVersion));
        ImageState restored = _undo.Last.Value;
        _undo.RemoveLast();
        restoredBitmap = restored.Bitmap;
        restoredVersion = restored.Version;
        return true;
    }

    public bool TryRedo(
        SKBitmap currentBitmap,
        long currentVersion,
        out SKBitmap? restoredBitmap,
        out long restoredVersion)
    {
        ArgumentNullException.ThrowIfNull(currentBitmap);

        if (!_redo.TryPop(out ImageState? restored))
        {
            restoredBitmap = null;
            restoredVersion = currentVersion;
            return false;
        }

        AddUndo(new ImageState(currentBitmap, currentVersion));
        restoredBitmap = restored.Bitmap;
        restoredVersion = restored.Version;
        return true;
    }

    public void Clear()
    {
        foreach (ImageState state in _undo)
        {
            state.Bitmap.Dispose();
        }

        _undo.Clear();
        ClearStack(_redo);
    }

    public void Dispose() => Clear();

    private void AddUndo(ImageState state)
    {
        _undo.AddLast(state);
        while (_undo.Count > Capacity)
        {
            ImageState oldest = _undo.First!.Value;
            _undo.RemoveFirst();
            oldest.Bitmap.Dispose();
        }
    }

    private static void ClearStack(Stack<ImageState> stack)
    {
        while (stack.TryPop(out ImageState? state))
        {
            state.Bitmap.Dispose();
        }
    }

    private sealed class ImageState(SKBitmap bitmap, long version)
    {
        public SKBitmap Bitmap { get; } = bitmap;

        public long Version { get; } = version;
    }
}
