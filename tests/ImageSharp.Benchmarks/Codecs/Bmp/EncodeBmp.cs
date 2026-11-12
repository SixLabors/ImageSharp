// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class EncodeBmp
{
    private FileStream bmpStream;
    private SDImage bmpDrawing;
    private Image<Rgba32> bmpCore;

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Bmp.Car));
            this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
            this.bmpStream.Position = 0;
            this.bmpDrawing = SDImage.FromStream(this.bmpStream);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpStream.Dispose();
        this.bmpStream = null;
        this.bmpCore.Dispose();
        this.bmpDrawing.Dispose();
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
    public void BmpSystemDrawing()
    {
        using MemoryStream memoryStream = new();
        this.bmpDrawing.Save(memoryStream, ImageFormat.Bmp);
    }

    [Benchmark(Description = "ImageSharp Bmp")]
    public void BmpImageSharp()
    {
        using MemoryStream memoryStream = new();
        this.bmpCore.SaveAsBmp(memoryStream);
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method               | Mean     | Error    | StdDev  | Ratio | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
    |--------------------- |---------:|---------:|--------:|------:|---------:|---------:|---------:|-----------:|------------:|
    | 'System.Drawing Bmp' | 823.4 us | 61.55 us | 3.37 us |  1.00 | 249.0234 | 249.0234 | 249.0234 |  794.95 KB |        1.00 |
    | 'ImageSharp Bmp'     | 365.0 us | 62.31 us | 3.42 us |  0.44 | 499.5117 | 499.5117 | 499.5117 | 1853.51 KB |        2.33 |

    Allocated reports managed memory only.
    Input: Bmp/Car.bmp.
    */
}
