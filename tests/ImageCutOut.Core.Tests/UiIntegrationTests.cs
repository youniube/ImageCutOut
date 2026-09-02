using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImageCutOut.Controls;
using ImageCutOut.Core;
using SkiaSharp;

namespace ImageCutOut.Core.Tests;

public sealed class UiIntegrationTests
{
    [AvaloniaFact]
    public void MainWindow_StartsInViewModeAndRendersEmptyState()
    {
        var window = new MainWindow();
        window.Show();

        var canvas = window.FindControl<ImageCanvas>("EditorCanvas")!;
        var cutButton = window.FindControl<ToggleButton>("CutButton")!;
        var helpButton = window.FindControl<Button>("HelpButton")!;
        var emptyHelpButton = window.FindControl<Button>("EmptyHelpButton")!;
        var saveButton = window.FindControl<Button>("SaveButton")!;
        var undoButton = window.FindControl<Button>("UndoButton")!;
        var emptyState = window.FindControl<Border>("EmptyState")!;
        var dimensions = window.FindControl<TextBlock>("DimensionsText")!;
        var status = window.FindControl<TextBlock>("StatusText")!;

        Assert.Equal("中间裁切", window.Title);
        Assert.False(canvas.IsCutToolActive);
        Assert.False(cutButton.IsChecked);
        Assert.False(cutButton.IsEnabled);
        Assert.True(helpButton.IsEnabled);
        Assert.True(emptyHelpButton.IsEnabled);
        Assert.False(saveButton.IsEnabled);
        Assert.False(undoButton.IsEnabled);
        Assert.True(emptyState.IsVisible);
        Assert.Equal("未打开图片", dimensions.Text);
        Assert.Equal("拖入图片或点击打开", status.Text);
        Assert.Contains("上下拖动", MainWindow.QuickStartHelpText);
        Assert.Contains("Ctrl+S", MainWindow.QuickStartHelpText);

        using Avalonia.Media.Imaging.Bitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string path = GetArtifactPath("ui-empty-state.png");
        frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(path).Length > 0);
        window.Close();
    }

    [AvaloniaFact]
    public void MainWindow_F1ShowsHelpDialog()
    {
        var window = new MainWindow();
        window.Show();

        window.KeyPress(Key.F1, RawInputModifiers.None, PhysicalKey.F1, null);

        Window helpWindow = Assert.Single(window.OwnedWindows);
        Assert.Equal("使用帮助", helpWindow.Title);
        Assert.True(helpWindow.IsVisible);
        using Avalonia.Media.Imaging.Bitmap? frame = helpWindow.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string path = GetArtifactPath("ui-help-dialog.png");
        frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(path).Length > 0);

        helpWindow.Close();
        window.Close();
    }

    [AvaloniaFact]
    public void Canvas_MapsQuarterScaleDragAndKeepsCutPendingUntilConfirmation()
    {
        var canvas = new ImageCanvas();
        var window = new Window
        {
            Width = 1008,
            Height = 588,
            Content = canvas
        };
        using SKBitmap source = BitmapTestFactory.CreatePattern(3840, 2160);
        window.Show();
        canvas.SetBitmap(source, resetView: true);
        canvas.IsCutToolActive = true;
        window.CaptureRenderedFrame()?.Dispose();

        double scale = canvas.DisplayScale;
        Assert.InRange(scale, 0.24, 0.26);
        double offsetX = (canvas.Bounds.Width - source.Width * scale) / 2;
        double offsetY = (canvas.Bounds.Height - source.Height * scale) / 2;
        double x = offsetX + source.Width * scale / 2;
        var start = new Point(x, offsetY + 800 * scale);
        var end = new Point(x, offsetY + 1200 * scale);

        window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(end, RawInputModifiers.None);
        window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);

        CutSelection selection = Assert.IsType<CutSelection>(canvas.PendingSelection);
        Assert.Equal(CutDirection.Horizontal, selection.Direction);
        Assert.InRange(selection.Start, 799, 801);
        Assert.InRange(selection.End, 1199, 1201);
        Assert.Equal(400, selection.Length);

        using Avalonia.Media.Imaging.Bitmap? selectionFrame = window.CaptureRenderedFrame();
        Assert.NotNull(selectionFrame);
        string selectionPath = GetArtifactPath("ui-cut-selection.png");
        selectionFrame.Save(selectionPath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(selectionPath).Length > 0);

        bool confirmationRequested = false;
        canvas.ConfirmRequested += (_, _) => confirmationRequested = true;
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.True(confirmationRequested);
        Assert.NotNull(canvas.PendingSelection);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(canvas.PendingSelection);
        Assert.True(canvas.IsCutToolActive);

        var wholeStart = new Point(x, offsetY);
        var wholeEnd = new Point(x, offsetY + source.Height * scale + 50);
        window.MouseDown(wholeStart, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(wholeEnd, RawInputModifiers.None);
        window.MouseUp(wholeEnd, MouseButton.Left, RawInputModifiers.None);
        Assert.Equal(source.Height, canvas.PendingSelection?.Length);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        window.Close();
    }

    [AvaloniaFact]
    public void Canvas_ZoomAndPanOnlyChangeTheView()
    {
        var canvas = new ImageCanvas();
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = canvas
        };
        using SKBitmap source = BitmapTestFactory.CreatePattern(1600, 1200);
        window.Show();
        canvas.SetBitmap(source, resetView: true);
        window.CaptureRenderedFrame()?.Dispose();

        double fittedScale = canvas.DisplayScale;
        var center = new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2);
        window.MouseWheel(center, new Vector(0, 1), RawInputModifiers.Control);
        Assert.True(canvas.DisplayScale > fittedScale);
        Assert.Equal((1600, 1200), (source.Width, source.Height));

        canvas.FitToWindow();
        double scaleBeforePan = canvas.DisplayScale;
        canvas.SetSpacePressed(true);
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(center + new Vector(40, 30), RawInputModifiers.None);
        window.MouseUp(center + new Vector(40, 30), MouseButton.Left, RawInputModifiers.None);
        canvas.SetSpacePressed(false);

        Assert.Equal(scaleBeforePan, canvas.DisplayScale, precision: 6);
        Assert.Equal((1600, 1200), (source.Width, source.Height));
        window.Close();
    }

    [AvaloniaFact]
    public async Task MainWindow_LoadsAndCompletesContinuousCutsWithUndo()
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "ImageCutOut.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string path = Path.Combine(tempDirectory, "workflow.png");
        using (SKBitmap source = BitmapTestFactory.CreatePattern(400, 300))
        {
            new ImageFileService().Save(source, path);
        }

        var window = new MainWindow();
        try
        {
            window.Show();
            await window.LoadImageAsync(path);

            var canvas = window.FindControl<ImageCanvas>("EditorCanvas")!;
            var cutButton = window.FindControl<ToggleButton>("CutButton")!;
            var undoButton = window.FindControl<Button>("UndoButton")!;
            var redoButton = window.FindControl<Button>("RedoButton")!;
            var dimensions = window.FindControl<TextBlock>("DimensionsText")!;
            var status = window.FindControl<TextBlock>("StatusText")!;
            var confirmationBar = window.FindControl<Border>("ConfirmationBar")!;

            Assert.Equal("workflow.png", window.Title);
            Assert.Equal("400 × 300", dimensions.Text);
            Assert.True(cutButton.IsEnabled);
            Assert.False(canvas.IsCutToolActive);
            Assert.Equal("下一步：点击“中间裁切”（C），然后在图片上拖动", status.Text);

            cutButton.IsChecked = true;
            cutButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(canvas.IsCutToolActive);

            PerformCanvasDrag(window, canvas, new Size(400, 300), new Point(200, 100), new Point(200, 180));
            Assert.True(confirmationBar.IsVisible);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Assert.Equal("400 × 220", dimensions.Text);
            Assert.StartsWith("* ", window.Title);
            Assert.True(canvas.IsCutToolActive);
            Assert.True(undoButton.IsEnabled);

            PerformCanvasDrag(window, canvas, new Size(400, 220), new Point(120, 110), new Point(170, 110));
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Assert.Equal("350 × 220", dimensions.Text);
            Assert.True(canvas.IsCutToolActive);

            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
            Assert.Equal("400 × 220", dimensions.Text);
            Assert.True(redoButton.IsEnabled);
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
            Assert.Equal("400 × 300", dimensions.Text);
            Assert.Equal("workflow.png", window.Title);
            Assert.False(undoButton.IsEnabled);
        }
        finally
        {
            window.Close();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string GetArtifactPath(string fileName)
    {
        DirectoryInfo root = new(AppContext.BaseDirectory);
        for (int i = 0; i < 5; i++)
        {
            root = root.Parent ?? throw new DirectoryNotFoundException("无法定位测试项目根目录。 ");
        }

        string artifacts = Path.Combine(root.FullName, "artifacts");
        Directory.CreateDirectory(artifacts);
        return Path.Combine(artifacts, fileName);
    }

    private static void PerformCanvasDrag(
        Window window,
        ImageCanvas canvas,
        Size imageSize,
        Point startImagePoint,
        Point endImagePoint)
    {
        double scale = canvas.DisplayScale;
        double offsetX = (canvas.Bounds.Width - imageSize.Width * scale) / 2;
        double offsetY = (canvas.Bounds.Height - imageSize.Height * scale) / 2;
        Point startLocal = new(
            offsetX + startImagePoint.X * scale,
            offsetY + startImagePoint.Y * scale);
        Point endLocal = new(
            offsetX + endImagePoint.X * scale,
            offsetY + endImagePoint.Y * scale);
        Point start = canvas.TranslatePoint(startLocal, window)
            ?? throw new InvalidOperationException("无法将画布坐标转换到窗口。 ");
        Point end = canvas.TranslatePoint(endLocal, window)
            ?? throw new InvalidOperationException("无法将画布坐标转换到窗口。 ");

        window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(end, RawInputModifiers.None);
        window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
        Assert.NotNull(canvas.PendingSelection);
    }
}
