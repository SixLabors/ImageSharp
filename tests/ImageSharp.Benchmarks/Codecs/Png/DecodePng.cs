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

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                | TestImage       |       Mean |       Error |      StdDev |   Ratio |   Allocated |   Alloc Ratio |
    | --------------------- | --------------- | ---------: | ----------: | ----------: | ------: | ----------: | ------------: |
    | 'System.Drawing Png'  | Png/splash.png  |   1.259 ms |   0.1887 ms |   0.0103 ms |    1.00 |       393 B |          1.00 |
    | 'ImageSharp Png'      | Png/splash.png  |   1.051 ms |   0.0469 ms |   0.0026 ms |    0.83 |      7776 B |         19.79 |

    Allocated reports managed memory only.
    */
}
