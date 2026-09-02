# Windows 图片中间裁切工具

一个面向 Windows 10/11 的轻量图片工具，用来删除图片中间的一整条横向或纵向区域，并将两侧像素无缝合拢。

## 使用

程序内置快速上手说明：启动页会直接显示操作步骤，点击工具栏“使用帮助”或按 `F1` 可以随时查看完整说明。

1. 拖入图片，或点击“打开”。
2. 点击“中间裁切”（快捷键 `C`）。
3. 在图片上上下拖动以删除横条，或左右拖动以删除竖条。
4. 检查遮罩、删除像素数和结果尺寸。
5. 按 `Enter` 或点击“确认删除”；按 `Esc` 取消当前选区。
6. 可连续裁切，完成后按 `Ctrl+S` 保存。

支持 PNG、JPG/JPEG、WebP、BMP；PNG 保存保留 Alpha，JPEG 默认质量为 95。Ctrl+鼠标滚轮缩放，`Space`+左键平移，`Ctrl+0` 适合窗口，`Ctrl+1` 显示实际大小。

## 构建与测试

需要 .NET 10 SDK：

```powershell
dotnet restore ImageCutOut.slnx --configfile NuGet.Config
dotnet test ImageCutOut.slnx -c Release
dotnet publish src\ImageCutOut.App\ImageCutOut.App.csproj -c Release -r win-x64 --self-contained true
```

当前自包含发布目录：

```text
artifacts/publish/win-x64/
```

## 代码结构

- `src/ImageCutOut.App/Core/CutOutOperation.cs`：逐行/逐列复制像素的核心裁切算法，不缩放、不重采样。
- `src/ImageCutOut.App/Core/ImageDocument.cs`：当前图片、版本号、脏状态和裁切事务。
- `src/ImageCutOut.App/Core/HistoryManager.cs`：最多 20 步的 Undo/Redo，负责及时释放历史位图。
- `src/ImageCutOut.App/Core/ImageFileService.cs`：图片解码、EXIF Orientation、格式编码和同目录临时文件替换。
- `src/ImageCutOut.App/Controls/ImageCanvas.cs`：显示、坐标转换、缩放、平移、选区和 Overlay。
- `tests/ImageCutOut.Core.Tests/`：像素、历史、格式、EXIF、大图和 Headless UI 集成测试。

实现研究过 ShareX 的 CutOut、历史 memento 和画布尺寸更新思路，但没有复制或复用 ShareX 源码。
