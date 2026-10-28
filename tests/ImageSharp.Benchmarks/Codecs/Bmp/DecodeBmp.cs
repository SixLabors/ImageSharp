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

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                | TestImage    |        Mean |       Error |     StdDev |   Ratio |   RatioSD |     Gen0 |     Gen1 |     Gen2 |   Allocated |   Alloc Ratio |
    | --------------------- | ------------ | ----------: | ----------: | ---------: | ------: | --------: | -------: | -------: | -------: | ----------: | ------------: |
    | 'System.Drawing Bmp'  | Bmp/Car.bmp  |   230.24 μs |   53.155 μs |   2.914 μs |    1.00 |      0.02 |        - |        - |        - |       392 B |          1.00 |
    | 'ImageSharp Bmp'      | Bmp/Car.bmp  |    40.66 μs |    2.761 μs |   0.151 μs |    0.18 |      0.00 |   0.1831 |   0.0610 |   0.0610 |      2289 B |          5.84 |

    Allocated reports managed memory only.
    */
}
