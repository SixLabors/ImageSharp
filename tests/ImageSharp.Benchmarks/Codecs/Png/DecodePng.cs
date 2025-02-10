// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class DecodePng
{
    private byte[] pngBytes;

    private string TestImageFullPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Png.Splash)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
        => this.pngBytes ??= File.ReadAllBytes(this.TestImageFullPath);

    [Benchmark(Baseline = true, Description = "System.Drawing Png")]
    public SDSize PngSystemDrawing()
    {
        using MemoryStream memoryStream = new(this.pngBytes);
        using SDImage image = SDImage.FromStream(memoryStream);
        return image.Size;
    }

    [Benchmark(Description = "ImageSharp Png")]
    public Size PngImageSharp()
    {
        using MemoryStream memoryStream = new(this.pngBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return image.Size;
    }
}
