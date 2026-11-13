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

    // Both libraries write into this stream. Each call rewinds it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    private MemoryStream outputStream;

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Bmp.Car));
            this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
            this.bmpStream.Position = 0;
            this.bmpDrawing = SDImage.FromStream(this.bmpStream);
            this.outputStream = new MemoryStream();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpStream.Dispose();
        this.bmpStream = null;
        this.bmpCore.Dispose();
        this.bmpDrawing.Dispose();
        this.outputStream.Dispose();
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
    public void BmpSystemDrawing()
    {
        this.outputStream.Position = 0;
        this.bmpDrawing.Save(this.outputStream, ImageFormat.Bmp);
    }

    [Benchmark(Description = "ImageSharp Bmp")]
    public void BmpImageSharp()
    {
        this.outputStream.Position = 0;
        this.bmpCore.SaveAsBmp(this.outputStream);
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
    |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|
    | 'System.Drawing Bmp' | 472.85 us | 266.669 us | 14.617 us |  1.00 |    0.04 |    3256 B |        1.00 |
    | 'ImageSharp Bmp'     |  22.82 us |   2.609 us |  0.143 us |  0.05 |    0.00 |     280 B |        0.09 |

    Allocated reports managed memory only.
    Input: Bmp/Car.bmp.
    */
}
