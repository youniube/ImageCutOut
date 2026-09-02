using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ImageCutOut.Core;
using SkiaSharp;

namespace ImageCutOut.Controls;

public sealed class ImageCanvas : Control
{
    private const double ViewPadding = 24;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 8;
    private static readonly IBrush OverlayBrush = new SolidColorBrush(Color.FromArgb(112, 239, 68, 68));
    private static readonly IBrush OverlayBorderBrush = new SolidColorBrush(Color.FromRgb(255, 110, 110));
    private static readonly IBrush LabelBackgroundBrush = new SolidColorBrush(Color.FromArgb(224, 28, 32, 40));
    private static readonly IBrush LabelForegroundBrush = Brushes.White;
    private static readonly IPen ImageBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1);
    private static readonly IPen OverlayBorderPen = new Pen(OverlayBorderBrush, 1.5);

    private WriteableBitmap? _displayBitmap;
    private int _imageWidth;
    private int _imageHeight;
    private bool _fitMode = true;
    private double _zoom = 1;
    private Vector _pan;
    private bool _spacePressed;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panAtStart;
    private bool _isSelecting;
    private Point _selectionStartImage;
    private CutSelection? _selection;
    private bool _selectionComplete;
    private bool _isCutToolActive;

    public ImageCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        SizeChanged += (_, _) =>
        {
            InvalidateVisual();
            RaiseZoomChanged();
        };
    }

    public event EventHandler<CutSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler? ConfirmRequested;

    public event EventHandler? ZoomChanged;

    public bool HasImage => _displayBitmap is not null;

    public bool IsCutToolActive
    {
        get => _isCutToolActive;
        set
        {
            if (_isCutToolActive == value)
            {
                return;
            }

            _isCutToolActive = value;
            Cursor = value ? new Cursor(StandardCursorType.Cross) : null;
            if (!value)
            {
                CancelSelection();
            }

            InvalidateVisual();
        }
    }

    public double DisplayScale => GetDisplayScale();

    public int ZoomPercent => (int)Math.Round(DisplayScale * 100);

    public CutSelection? PendingSelection => _selectionComplete ? _selection : null;

    public void SetBitmap(SKBitmap? bitmap, bool resetView)
    {
        _displayBitmap?.Dispose();
        _displayBitmap = null;
        _imageWidth = 0;
        _imageHeight = 0;
        CancelSelection();

        if (bitmap is not null)
        {
            _imageWidth = bitmap.Width;
            _imageHeight = bitmap.Height;
            _displayBitmap = CreateDisplayBitmap(bitmap);
        }

        if (resetView)
        {
            FitToWindow();
        }

        InvalidateVisual();
        RaiseZoomChanged();
    }

    public void FitToWindow()
    {
        _fitMode = true;
        _pan = default;
        InvalidateVisual();
        RaiseZoomChanged();
    }

    public void ActualSize()
    {
        _fitMode = false;
        _zoom = 1;
        _pan = default;
        InvalidateVisual();
        RaiseZoomChanged();
    }

    public void SetSpacePressed(bool isPressed)
    {
        _spacePressed = isPressed;
    }

    public void CancelSelection()
    {
        bool hadSelection = _selection is not null || _isSelecting;
        _isSelecting = false;
        _selection = null;
        _selectionComplete = false;
        if (hadSelection)
        {
            SelectionChanged?.Invoke(this, new CutSelectionChangedEventArgs(null, false));
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_displayBitmap is null)
        {
            return;
        }

        Rect imageRect = GetImageRect();
        context.DrawImage(
            _displayBitmap,
            new Rect(0, 0, _imageWidth, _imageHeight),
            imageRect);
        context.DrawRectangle(null, ImageBorderPen, imageRect);

        if (_selection is { } selection)
        {
            DrawSelection(context, imageRect, selection);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        PointerPoint currentPoint = e.GetCurrentPoint(this);
        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point position = currentPoint.Position;
        if (_spacePressed && HasImage)
        {
            if (_fitMode)
            {
                _zoom = GetDisplayScale();
                _fitMode = false;
            }

            _isPanning = true;
            _panStart = position;
            _panAtStart = _pan;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!_isCutToolActive || !TryScreenToImage(position, requireInside: true, out Point imagePoint))
        {
            return;
        }

        _isSelecting = true;
        _selectionComplete = false;
        _selectionStartImage = imagePoint;
        _selection = null;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);

        if (_isPanning)
        {
            _pan = _panAtStart + (position - _panStart);
            _fitMode = false;
            InvalidateVisual();
            return;
        }

        if (!_isSelecting || !TryScreenToImage(position, requireInside: false, out Point imagePoint))
        {
            return;
        }

        _selection = CreateSelection(_selectionStartImage, imagePoint);
        _selectionComplete = false;
        SelectionChanged?.Invoke(this, new CutSelectionChangedEventArgs(_selection, false));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        e.Pointer.Capture(null);
        if (TryScreenToImage(e.GetPosition(this), requireInside: false, out Point imagePoint))
        {
            _selection = CreateSelection(_selectionStartImage, imagePoint);
        }

        if (_selection is not { Length: >= 2 } selection)
        {
            CancelSelection();
            e.Handled = true;
            return;
        }

        _selectionComplete = true;
        SelectionChanged?.Invoke(this, new CutSelectionChangedEventArgs(selection, true));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!HasImage || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0)
        {
            return;
        }

        ZoomAt(e.GetPosition(this), e.Delta.Y > 0 ? 1.15 : 1 / 1.15);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space)
        {
            SetSpacePressed(true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isCutToolActive)
        {
            CancelSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && PendingSelection is not null)
        {
            ConfirmRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            SetSpacePressed(false);
            e.Handled = true;
        }
    }

    private static unsafe WriteableBitmap CreateDisplayBitmap(SKBitmap source)
    {
        if (source.ColorType != SKColorType.Bgra8888 || source.AlphaType != SKAlphaType.Premul)
        {
            throw new InvalidOperationException("画布只接受 BGRA8888 Premul 图片。 ");
        }

        var target = new WriteableBitmap(
            new PixelSize(source.Width, source.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        using ILockedFramebuffer frameBuffer = target.Lock();
        byte* sourceBase = (byte*)source.GetPixels();
        byte* destinationBase = (byte*)frameBuffer.Address;
        int bytesPerRow = checked(source.Width * source.BytesPerPixel);

        for (int y = 0; y < source.Height; y++)
        {
            Buffer.MemoryCopy(
                sourceBase + y * source.RowBytes,
                destinationBase + y * frameBuffer.RowBytes,
                frameBuffer.RowBytes,
                bytesPerRow);
        }

        return target;
    }

    private void ZoomAt(Point screenPoint, double factor)
    {
        Rect oldRect = GetImageRect();
        double oldScale = GetDisplayScale();
        double imageX = (screenPoint.X - oldRect.X) / oldScale;
        double imageY = (screenPoint.Y - oldRect.Y) / oldScale;
        _fitMode = false;
        _zoom = Math.Clamp(oldScale * factor, MinZoom, MaxZoom);

        double centeredX = (Bounds.Width - _imageWidth * _zoom) / 2;
        double centeredY = (Bounds.Height - _imageHeight * _zoom) / 2;
        _pan = new Vector(
            screenPoint.X - imageX * _zoom - centeredX,
            screenPoint.Y - imageY * _zoom - centeredY);
        InvalidateVisual();
        RaiseZoomChanged();
    }

    private CutSelection CreateSelection(Point start, Point end)
    {
        double deltaX = Math.Abs(end.X - start.X);
        double deltaY = Math.Abs(end.Y - start.Y);
        CutDirection direction = deltaY > deltaX ? CutDirection.Horizontal : CutDirection.Vertical;
        int startPixel = direction == CutDirection.Horizontal
            ? ToPixel(start.Y, _imageHeight)
            : ToPixel(start.X, _imageWidth);
        int endPixel = direction == CutDirection.Horizontal
            ? ToPixel(end.Y, _imageHeight)
            : ToPixel(end.X, _imageWidth);
        return new CutSelection(direction, startPixel, endPixel);
    }

    private static int ToPixel(double coordinate, int maximum) =>
        Math.Clamp((int)Math.Round(coordinate, MidpointRounding.AwayFromZero), 0, maximum);

    private bool TryScreenToImage(Point screenPoint, bool requireInside, out Point imagePoint)
    {
        imagePoint = default;
        if (!HasImage)
        {
            return false;
        }

        Rect imageRect = GetImageRect();
        if (requireInside && !imageRect.Contains(screenPoint))
        {
            return false;
        }

        (double x, double y) = ImageCoordinateMapper.ScreenToImage(
            screenPoint.X,
            screenPoint.Y,
            imageRect.X,
            imageRect.Y,
            GetDisplayScale(),
            _imageWidth,
            _imageHeight);
        imagePoint = new Point(x, y);
        return true;
    }

    private double GetDisplayScale()
    {
        if (!HasImage || _imageWidth == 0 || _imageHeight == 0)
        {
            return 1;
        }

        if (!_fitMode)
        {
            return Math.Clamp(_zoom, MinZoom, MaxZoom);
        }

        double availableWidth = Math.Max(1, Bounds.Width - ViewPadding * 2);
        double availableHeight = Math.Max(1, Bounds.Height - ViewPadding * 2);
        return Math.Min(1, Math.Min(availableWidth / _imageWidth, availableHeight / _imageHeight));
    }

    private Rect GetImageRect()
    {
        double scale = GetDisplayScale();
        double width = _imageWidth * scale;
        double height = _imageHeight * scale;
        return new Rect(
            (Bounds.Width - width) / 2 + _pan.X,
            (Bounds.Height - height) / 2 + _pan.Y,
            width,
            height);
    }

    private void DrawSelection(DrawingContext context, Rect imageRect, CutSelection selection)
    {
        double scale = GetDisplayScale();
        Rect overlayRect = selection.Direction == CutDirection.Horizontal
            ? new Rect(imageRect.X, imageRect.Y + selection.Start * scale, imageRect.Width, selection.Length * scale)
            : new Rect(imageRect.X + selection.Start * scale, imageRect.Y, selection.Length * scale, imageRect.Height);
        context.DrawRectangle(OverlayBrush, OverlayBorderPen, overlayRect);

        int resultWidth = selection.Direction == CutDirection.Vertical
            ? _imageWidth - selection.Length
            : _imageWidth;
        int resultHeight = selection.Direction == CutDirection.Horizontal
            ? _imageHeight - selection.Length
            : _imageHeight;
        var label = new FormattedText(
            $"删除 {selection.Length} px\n结果：{resultWidth} × {resultHeight}",
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            13,
            LabelForegroundBrush);
        const double labelPadding = 8;
        double labelWidth = label.Width + labelPadding * 2;
        double labelHeight = label.Height + labelPadding * 2;
        double labelX = Math.Clamp(
            overlayRect.Center.X - labelWidth / 2,
            4,
            Math.Max(4, Bounds.Width - labelWidth - 4));
        double preferredY = overlayRect.Bottom + 6;
        if (preferredY + labelHeight > Bounds.Height - 4)
        {
            preferredY = overlayRect.Top - labelHeight - 6;
        }

        double labelY = Math.Clamp(preferredY, 4, Math.Max(4, Bounds.Height - labelHeight - 4));
        var labelRect = new Rect(labelX, labelY, labelWidth, labelHeight);
        context.DrawRectangle(LabelBackgroundBrush, null, labelRect, 5, 5);
        context.DrawText(label, new Point(labelX + labelPadding, labelY + labelPadding));
    }

    private void RaiseZoomChanged() => ZoomChanged?.Invoke(this, EventArgs.Empty);
}
