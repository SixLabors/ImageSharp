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

    private string TestImageFullPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [GlobalSetup]
    public void ReadImages()
        => this.bmpBytes ??= File.ReadAllBytes(this.TestImageFullPath);

    [Params(TestImages.Bmp.Car)]
    public string TestImage { get; set; }

    [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
    public SDSize BmpSystemDrawing()
    {
        using MemoryStream memoryStream = new(this.bmpBytes);
        using SDImage image = SDImage.FromStream(memoryStream);
        return image.Size;
    }

    [Benchmark(Description = "ImageSharp Bmp")]
    public Size BmpImageSharp()
    {
        using MemoryStream memoryStream = new(this.bmpBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
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

    | Method               | TestImage   | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
    |--------------------- |------------ |----------:|---------:|---------:|------:|-------:|----------:|------------:|
    | 'System.Drawing Bmp' | Bmp/Car.bmp | 255.49 us | 27.43 us | 1.504 us |  1.00 |      - |     392 B |        1.00 |
    | 'ImageSharp Bmp'     | Bmp/Car.bmp |  37.97 us | 10.94 us | 0.600 us |  0.15 | 0.1221 |    2320 B |        5.92 |

    Allocated reports managed memory only.
    */
}
