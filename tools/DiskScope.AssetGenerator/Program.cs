using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: DiskScope.AssetGenerator <source-png> <ico-output> <preview-png-output>");
    return 2;
}

var sourcePath = Path.GetFullPath(args[0]);
if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"Source image does not exist: {sourcePath}");
    return 2;
}

BitmapFrame source;
using (var sourceStream = File.OpenRead(sourcePath))
{
    source = BitmapFrame.Create(
        sourceStream,
        BitmapCreateOptions.PreservePixelFormat,
        BitmapCacheOption.OnLoad);
}

var iconSizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
var iconFrames = iconSizes.Select(size => EncodePng(RenderSquare(source, size))).ToArray();
var iconPath = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
using (var iconStream = File.Create(iconPath))
using (var writer = new BinaryWriter(iconStream))
{
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)iconFrames.Length);

    var offset = 6 + iconFrames.Length * 16;
    for (var index = 0; index < iconFrames.Length; index++)
    {
        var size = iconSizes[index];
        writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(iconFrames[index].Length);
        writer.Write(offset);
        offset += iconFrames[index].Length;
    }

    foreach (var frame in iconFrames) writer.Write(frame);
}

var previewPath = Path.GetFullPath(args[2]);
WriteFile(previewPath, EncodePng(RenderSquare(source, 512)));

Console.WriteLine($"Generated {iconPath} with {iconFrames.Length} sizes");
Console.WriteLine($"Generated {previewPath}");
return 0;

static RenderTargetBitmap RenderSquare(BitmapSource source, int size)
{
    var visual = new DrawingVisual();
    RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
    using (var drawing = visual.RenderOpen())
    {
        var padding = Math.Max(1, size * 0.035);
        drawing.DrawImage(source, new Rect(padding, padding, size - padding * 2, size - padding * 2));
    }

    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    bitmap.Freeze();
    return bitmap;
}

static byte[] EncodePng(BitmapSource bitmap)
{
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = new MemoryStream();
    encoder.Save(stream);
    return stream.ToArray();
}

static void WriteFile(string path, byte[] content)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, content);
}
