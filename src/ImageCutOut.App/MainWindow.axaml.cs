using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ImageCutOut.Controls;
using ImageCutOut.Core;
using ImageCutOut.Dialogs;
using SkiaSharp;

namespace ImageCutOut;

public sealed partial class MainWindow : Window
{
    internal const string QuickStartHelpText = """
快速开始

1. 点击“打开”，或把 PNG、JPG、JPEG、WebP、BMP 图片拖进窗口。
2. 点击“中间裁切”，或按 C 开启裁切工具。
3. 在图片上按住左键拖动：
   · 上下拖动：删除一整条横向区域
   · 左右拖动：删除一整条纵向区域
4. 松开鼠标后检查红色遮罩和结果尺寸。
5. 按 Enter 或点击“确认删除”；按 Esc 取消当前选区。
6. 可以连续裁切；Ctrl+Z 撤销，Ctrl+Y 重做。
7. Ctrl+S 保存，Ctrl+Shift+S 另存为。

查看图片

· Ctrl+鼠标滚轮：缩放
· Space+左键拖动：平移
· Ctrl+0：适合窗口
· Ctrl+1：实际大小

提示：第一次覆盖原文件时会先询问。建议先用图片副本熟悉操作。
""";

    private static readonly FilePickerFileType ImageFiles = new("图片")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
    };

    private static readonly IReadOnlyList<FilePickerFileType> SaveFileTypes =
    [
        new FilePickerFileType("PNG 图片") { Patterns = ["*.png"] },
        new FilePickerFileType("JPEG 图片") { Patterns = ["*.jpg", "*.jpeg"] },
        new FilePickerFileType("WebP 图片") { Patterns = ["*.webp"] },
        new FilePickerFileType("BMP 图片") { Patterns = ["*.bmp"] }
    ];

    private readonly ImageFileService _fileService = new();
    private readonly HashSet<string> _overwriteConfirmedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ImageCanvas _canvas;
    private readonly Button _openButton;
    private readonly ToggleButton _cutButton;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _saveButton;
    private readonly Button _saveAsButton;
    private readonly Button _actualSizeButton;
    private readonly Button _fitButton;
    private readonly Button _helpButton;
    private readonly Button _emptyOpenButton;
    private readonly Button _emptyHelpButton;
    private readonly Border _emptyState;
    private readonly Border _confirmationBar;
    private readonly TextBlock _confirmationText;
    private readonly TextBlock _dimensionsText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _zoomText;
    private ImageDocument? _document;
    private bool _isBusy;
    private bool _allowClose;
    private bool _closePromptActive;

    public MainWindow()
    {
        InitializeComponent();

        _canvas = this.FindControl<ImageCanvas>("EditorCanvas")!;
        _openButton = this.FindControl<Button>("OpenButton")!;
        _cutButton = this.FindControl<ToggleButton>("CutButton")!;
        _undoButton = this.FindControl<Button>("UndoButton")!;
        _redoButton = this.FindControl<Button>("RedoButton")!;
        _saveButton = this.FindControl<Button>("SaveButton")!;
        _saveAsButton = this.FindControl<Button>("SaveAsButton")!;
        _actualSizeButton = this.FindControl<Button>("ActualSizeButton")!;
        _fitButton = this.FindControl<Button>("FitButton")!;
        _helpButton = this.FindControl<Button>("HelpButton")!;
        _emptyOpenButton = this.FindControl<Button>("EmptyOpenButton")!;
        _emptyHelpButton = this.FindControl<Button>("EmptyHelpButton")!;
        _emptyState = this.FindControl<Border>("EmptyState")!;
        _confirmationBar = this.FindControl<Border>("ConfirmationBar")!;
        _confirmationText = this.FindControl<TextBlock>("ConfirmationText")!;
        _dimensionsText = this.FindControl<TextBlock>("DimensionsText")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _zoomText = this.FindControl<TextBlock>("ZoomText")!;

        _openButton.Click += OpenClicked;
        _emptyOpenButton.Click += OpenClicked;
        _cutButton.Click += (_, _) => SetCutToolActive(_cutButton.IsChecked == true);
        _undoButton.Click += (_, _) => Undo();
        _redoButton.Click += (_, _) => Redo();
        _saveButton.Click += async (_, _) => await SaveAsync();
        _saveAsButton.Click += async (_, _) => await SaveAsAsync();
        _actualSizeButton.Click += (_, _) => _canvas.ActualSize();
        _fitButton.Click += (_, _) => _canvas.FitToWindow();
        _helpButton.Click += async (_, _) => await ShowHelpAsync();
        _emptyHelpButton.Click += async (_, _) => await ShowHelpAsync();
        this.FindControl<Button>("ConfirmCutButton")!.Click += (_, _) => ConfirmCut();
        this.FindControl<Button>("CancelCutButton")!.Click += (_, _) => _canvas.CancelSelection();
        _canvas.SelectionChanged += CanvasSelectionChanged;
        _canvas.ConfirmRequested += (_, _) => ConfirmCut();
        _canvas.ZoomChanged += (_, _) => UpdateZoomText();

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => _canvas.SetSpacePressed(false);

        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
        Closing += OnClosing;
        Closed += (_, _) => _document?.Dispose();
        UpdateUiState();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _isBusy)
        {
            return;
        }

        bool control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (control && shift && e.Key == Key.S)
        {
            _ = SaveAsAsync();
        }
        else if (control && e.Key == Key.O)
        {
            _ = OpenFromPickerAsync();
        }
        else if (control && e.Key == Key.S)
        {
            _ = SaveAsync();
        }
        else if (control && e.Key == Key.Z)
        {
            Undo();
        }
        else if (control && e.Key == Key.Y)
        {
            Redo();
        }
        else if (control && e.Key == Key.D0)
        {
            _canvas.FitToWindow();
        }
        else if (control && e.Key == Key.D1)
        {
            _canvas.ActualSize();
        }
        else if (!control && !shift && e.Key == Key.F1)
        {
            _ = ShowHelpAsync();
        }
        else if (!control && !shift && e.Key == Key.C && _document is not null)
        {
            SetCutToolActive(!_canvas.IsCutToolActive);
        }
        else if (e.Key == Key.Escape && _canvas.IsCutToolActive)
        {
            _canvas.CancelSelection();
        }
        else if (e.Key == Key.Enter && _canvas.PendingSelection is not null)
        {
            ConfirmCut();
        }
        else
        {
            return;
        }

        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && !_isBusy)
        {
            _canvas.SetSpacePressed(true);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _canvas.SetSpacePressed(false);
            e.Handled = true;
        }
    }

    private async void OpenClicked(object? sender, RoutedEventArgs e) => await OpenFromPickerAsync();

    private async Task OpenFromPickerAsync()
    {
        if (_isBusy)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开图片",
            AllowMultiple = false,
            FileTypeFilter = [ImageFiles]
        });

        if (files.Count > 0)
        {
            await LoadImageAsync(files[0].Path.LocalPath);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_isBusy || e.DataTransfer.TryGetFiles() is not { } files)
        {
            return;
        }

        string? path = files.Select(file => file.Path.LocalPath).FirstOrDefault(_fileService.IsSupportedPath);
        if (path is null)
        {
            await ChoiceDialog.ShowErrorAsync(this, "请拖入 PNG、JPG、JPEG、WebP 或 BMP 图片。 ");
            return;
        }

        await LoadImageAsync(path);
    }

    internal async Task LoadImageAsync(string path)
    {
        if (!await ConfirmCanReplaceDocumentAsync())
        {
            return;
        }

        SetBusy(true, "正在打开图片…");
        SKBitmap? bitmap = null;
        try
        {
            bitmap = await Task.Run(() => _fileService.Load(path));
            var newDocument = new ImageDocument(bitmap, Path.GetFullPath(path));
            bitmap = null;
            _document?.Dispose();
            _document = newDocument;
            _overwriteConfirmedPaths.Remove(newDocument.FilePath!);
            SetCutToolActive(false);
            _canvas.SetBitmap(newDocument.CurrentBitmap, resetView: true);
        }
        catch (Exception ex)
        {
            bitmap?.Dispose();
            await ChoiceDialog.ShowErrorAsync(this, $"无法打开图片。\n\n{ex.Message}");
        }
        finally
        {
            SetBusy(false);
            UpdateUiState();
        }
    }

    private void SetCutToolActive(bool active)
    {
        active &= _document is not null && !_isBusy;
        _canvas.IsCutToolActive = active;
        _cutButton.IsChecked = active;
        _statusText.Text = active
            ? "拖动选择要删除的横向或纵向区域"
            : _document is null
                ? "拖入图片或点击打开"
                : "下一步：点击“中间裁切”（C），然后在图片上拖动";
        if (!active)
        {
            _confirmationBar.IsVisible = false;
        }
    }

    private void CanvasSelectionChanged(object? sender, CutSelectionChangedEventArgs e)
    {
        if (e.Selection is not { } selection || _document is null)
        {
            _confirmationBar.IsVisible = false;
            _statusText.Text = _canvas.IsCutToolActive
                ? "拖动选择要删除的横向或纵向区域"
                : string.Empty;
            return;
        }

        string description = GetSelectionDescription(selection);
        _statusText.Text = description;
        _confirmationText.Text = description;
        _confirmationBar.IsVisible = e.IsComplete;
    }

    private string GetSelectionDescription(CutSelection selection)
    {
        if (_document is null)
        {
            return string.Empty;
        }

        int resultWidth = selection.Direction == CutDirection.Vertical
            ? _document.CurrentBitmap.Width - selection.Length
            : _document.CurrentBitmap.Width;
        int resultHeight = selection.Direction == CutDirection.Horizontal
            ? _document.CurrentBitmap.Height - selection.Length
            : _document.CurrentBitmap.Height;
        return $"删除 {selection.Length} px → {resultWidth} × {resultHeight}";
    }

    private void ConfirmCut()
    {
        if (_document is null || _canvas.PendingSelection is not { } selection)
        {
            return;
        }

        if (!_document.TryCut(selection, out string? error))
        {
            _canvas.CancelSelection();
            if (!string.IsNullOrWhiteSpace(error))
            {
                _ = ChoiceDialog.ShowErrorAsync(this, error);
            }

            return;
        }

        _canvas.SetBitmap(_document.CurrentBitmap, resetView: false);
        _confirmationBar.IsVisible = false;
        _statusText.Text = "拖动选择要删除的横向或纵向区域";
        UpdateUiState();
    }

    private void Undo()
    {
        if (_document?.Undo() != true)
        {
            return;
        }

        _canvas.SetBitmap(_document.CurrentBitmap, resetView: false);
        UpdateUiState();
    }

    private void Redo()
    {
        if (_document?.Redo() != true)
        {
            return;
        }

        _canvas.SetBitmap(_document.CurrentBitmap, resetView: false);
        UpdateUiState();
    }

    private async Task ShowHelpAsync()
    {
        await ChoiceDialog.ShowAsync(
            this,
            "使用帮助",
            QuickStartHelpText,
            new DialogChoice("知道了", "ok", true));
    }

    private async Task<bool> SaveAsync()
    {
        if (_document is null)
        {
            return false;
        }

        if (!_document.IsDirty)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_document.FilePath))
        {
            return await SaveAsAsync();
        }

        string path = _document.FilePath;
        if (!_overwriteConfirmedPaths.Contains(path))
        {
            string? choice = await ChoiceDialog.ShowAsync(
                this,
                "覆盖原文件？",
                $"将使用当前编辑结果覆盖：\n{path}",
                new DialogChoice("覆盖", "overwrite", true),
                new DialogChoice("另存为", "save-as"),
                new DialogChoice("取消", "cancel"));
            if (choice == "save-as")
            {
                return await SaveAsAsync();
            }

            if (choice != "overwrite")
            {
                return false;
            }

            _overwriteConfirmedPaths.Add(path);
        }

        return await SaveToPathAsync(path);
    }

    private async Task<bool> SaveAsAsync()
    {
        if (_document is null || _isBusy)
        {
            return false;
        }

        string suggestedName = string.IsNullOrWhiteSpace(_document.FilePath)
            ? "图片.png"
            : Path.GetFileName(_document.FilePath);
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "另存为",
            SuggestedFileName = suggestedName,
            DefaultExtension = Path.GetExtension(suggestedName).TrimStart('.') is { Length: > 0 } extension
                ? extension
                : "png",
            FileTypeChoices = SaveFileTypes,
            ShowOverwritePrompt = true
        });

        return file is not null && await SaveToPathAsync(file.Path.LocalPath);
    }

    private async Task<bool> SaveToPathAsync(string path)
    {
        if (_document is null)
        {
            return false;
        }

        long versionBeingSaved = _document.DocumentVersion;
        SetBusy(true, "正在保存…");
        try
        {
            await Task.Run(() => _fileService.Save(_document.CurrentBitmap, path));
            if (_document.DocumentVersion == versionBeingSaved)
            {
                _document.MarkSaved(Path.GetFullPath(path));
            }

            _statusText.Text = "已保存";
            return true;
        }
        catch (Exception ex)
        {
            await ChoiceDialog.ShowErrorAsync(this, $"无法保存图片。\n\n{ex.Message}");
            return false;
        }
        finally
        {
            SetBusy(false);
            UpdateUiState();
        }
    }

    private async Task<bool> ConfirmCanReplaceDocumentAsync()
    {
        if (_document?.IsDirty != true)
        {
            return true;
        }

        string? choice = await ChoiceDialog.ShowAsync(
            this,
            "当前图片尚未保存",
            "保存当前修改后再打开另一张图片吗？",
            new DialogChoice("保存", "save", true),
            new DialogChoice("不保存并打开", "discard"),
            new DialogChoice("取消", "cancel"));
        return choice switch
        {
            "save" => await SaveAsync(),
            "discard" => true,
            _ => false
        };
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose || _document?.IsDirty != true || _closePromptActive)
        {
            return;
        }

        e.Cancel = true;
        _closePromptActive = true;
        try
        {
            string? choice = await ChoiceDialog.ShowAsync(
                this,
                "是否保存修改？",
                "当前图片包含尚未保存的修改。",
                new DialogChoice("保存", "save", true),
                new DialogChoice("不保存", "discard"),
                new DialogChoice("取消", "cancel"));
            bool canClose = choice == "discard" || (choice == "save" && await SaveAsync());
            if (canClose)
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closePromptActive = false;
        }
    }

    private void SetBusy(bool isBusy, string? status = null)
    {
        _isBusy = isBusy;
        _canvas.IsEnabled = !isBusy;
        _openButton.IsEnabled = !isBusy;
        _emptyOpenButton.IsEnabled = !isBusy;
        _helpButton.IsEnabled = !isBusy;
        _emptyHelpButton.IsEnabled = !isBusy;
        if (status is not null)
        {
            _statusText.Text = status;
        }
    }

    private void UpdateUiState()
    {
        bool hasDocument = _document is not null;
        _emptyState.IsVisible = !hasDocument;
        _cutButton.IsEnabled = hasDocument && !_isBusy;
        _undoButton.IsEnabled = _document?.CanUndo == true && !_isBusy;
        _redoButton.IsEnabled = _document?.CanRedo == true && !_isBusy;
        _saveButton.IsEnabled = hasDocument && !_isBusy;
        _saveAsButton.IsEnabled = hasDocument && !_isBusy;
        _actualSizeButton.IsEnabled = hasDocument && !_isBusy;
        _fitButton.IsEnabled = hasDocument && !_isBusy;

        if (_document is null)
        {
            Title = "中间裁切";
            _dimensionsText.Text = "未打开图片";
            _zoomText.Text = string.Empty;
            if (!_isBusy)
            {
                _statusText.Text = "拖入图片或点击打开";
            }

            return;
        }

        string fileName = string.IsNullOrWhiteSpace(_document.FilePath)
            ? "未命名"
            : Path.GetFileName(_document.FilePath);
        Title = _document.IsDirty ? $"* {fileName}" : fileName;
        _dimensionsText.Text = $"{_document.CurrentBitmap.Width} × {_document.CurrentBitmap.Height}";
        UpdateZoomText();
    }

    private void UpdateZoomText()
    {
        _zoomText.Text = _document is null ? string.Empty : $"{_canvas.ZoomPercent}%";
    }
}
