using ImageCutOut.Core;

namespace ImageCutOut.Controls;

public sealed class CutSelectionChangedEventArgs(CutSelection? selection, bool isComplete) : EventArgs
{
    public CutSelection? Selection { get; } = selection;

    public bool IsComplete { get; } = isComplete;
}
