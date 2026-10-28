// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class EncodePng
{
    // System.Drawing needs this.
    private FileStream bmpStream;
    private SDImage bmpDrawing;
    private Image<Rgba32> bmpCore;

    [Params(false)]
    public bool LargeImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            string path = Path.Combine(
                TestEnvironment.InputImagesDirectoryFullPath,
                this.LargeImage ? TestImages.Jpeg.Baseline.Jpeg420Exif : TestImages.Bmp.Car);

            this.bmpStream = File.OpenRead(path);
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

    [Benchmark(Baseline = true, Description = "System.Drawing Png")]
    public void PngSystemDrawing()
    {
        using MemoryStream memoryStream = new();
        this.bmpDrawing.Save(memoryStream, ImageFormat.Png);
    }

    [Benchmark(Description = "ImageSharp Png")]
    public void PngCore()
    {
        using MemoryStream memoryStream = new();
        PngEncoder encoder = new() { FilterMethod = PngFilterMethod.None };
        this.bmpCore.SaveAsPng(memoryStream, encoder);
    }

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                | LargeImage  |       Mean |       Error |      StdDev |   Ratio |       Gen0 |       Gen1 |       Gen2 |   Allocated |   Alloc Ratio |
    | --------------------- | ----------- | ---------: | ----------: | ----------: | ------: | ---------: | ---------: | ---------: | ----------: | ------------: |
    | 'System.Drawing Png'  | False       |   6.545 ms |   0.4520 ms |   0.0248 ms |    1.00 |   281.2500 |   281.2500 |   281.2500 |   963.73 KB |          1.00 |
    | 'ImageSharp Png'      | False       |   6.930 ms |   0.8409 ms |   0.0461 ms |    1.06 |   281.2500 |   281.2500 |   281.2500 |   963.26 KB |          1.00 |

    Allocated reports managed memory only.
    LargeImage=False selects Bmp/Car.bmp. ImageSharp uses PngFilterMethod.None.
    */
}
