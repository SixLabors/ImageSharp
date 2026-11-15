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

    // Both libraries read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream inputStream;

    private string TestImageFullPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Png.Splash)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        this.pngBytes ??= File.ReadAllBytes(this.TestImageFullPath);
        this.inputStream ??= new MemoryStream(this.pngBytes, false);
    }

    /// <summary>
    /// Releases the shared input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Png")]
    public SDSize PngSystemDrawing()
    {
        this.inputStream.Position = 0;
        using SDImage image = SDImage.FromStream(this.inputStream);
        return image.Size;
    }

    [Benchmark(Description = "ImageSharp Png")]
    public Size PngImageSharp()
    {
        this.inputStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.inputStream);
        return image.Size;
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method               | TestImage      | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
    |--------------------- |--------------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
    | 'System.Drawing Png' | Png/splash.png | 1.378 ms | 1.0352 ms | 0.0567 ms |  1.00 |    0.05 |     329 B |        1.00 |
    | 'ImageSharp Png'     | Png/splash.png | 1.112 ms | 0.1993 ms | 0.0109 ms |  0.81 |    0.03 |    7744 B |       23.54 |

    Allocated reports managed memory only.
    */
}
