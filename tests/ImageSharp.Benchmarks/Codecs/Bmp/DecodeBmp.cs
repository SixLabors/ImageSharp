// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class DecodeBmp
{
    private byte[] bmpBytes;

    // Both libraries read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream inputStream;

    private string TestImageFullPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [GlobalSetup]
    public void ReadImages()
    {
        this.bmpBytes ??= File.ReadAllBytes(this.TestImageFullPath);
        this.inputStream ??= new MemoryStream(this.bmpBytes, false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
    }

    [Params(TestImages.Bmp.Car)]
    public string TestImage { get; set; }

    [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
    public SDSize BmpSystemDrawing()
    {
        this.inputStream.Position = 0;
        using SDImage image = SDImage.FromStream(this.inputStream);
        return image.Size;
    }

    [Benchmark(Description = "ImageSharp Bmp")]
    public Size BmpImageSharp()
    {
        this.inputStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.inputStream);
        return new Size(image.Width, image.Height);
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method               | TestImage   | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
    |--------------------- |------------ |----------:|---------:|---------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|
    | 'System.Drawing Bmp' | Bmp/Car.bmp | 248.13 us | 87.24 us | 4.782 us |  1.00 |    0.02 |      - |      - |      - |     328 B |        1.00 |
    | 'ImageSharp Bmp'     | Bmp/Car.bmp |  37.03 us | 14.96 us | 0.820 us |  0.15 |    0.00 | 0.1831 | 0.0610 | 0.0610 |    2257 B |        6.88 |

    Allocated reports managed memory only.
    */
}
