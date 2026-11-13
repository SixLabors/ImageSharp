// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class EncodeTga
{
    private MagickImage tgaMagick;
    private Image<Rgba32> tga;

    // Both libraries write into this stream. Each call rewinds it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    private MemoryStream outputStream;

    private string TestImageFullPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Tga.Bit24BottomLeft)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.tga == null)
        {
            this.tga = Image.Load<Rgba32>(this.TestImageFullPath);
            this.tgaMagick = new MagickImage(this.TestImageFullPath);
            this.outputStream = new MemoryStream();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.tga.Dispose();
        this.tga = null;
        this.tgaMagick.Dispose();
        this.outputStream.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Magick Tga")]
    public void MagickTga()
    {
        this.outputStream.Position = 0;
        this.tgaMagick.Write(this.outputStream, MagickFormat.Tga);
    }

    [Benchmark(Description = "ImageSharp Tga")]
    public void ImageSharpTga()
    {
        this.outputStream.Position = 0;
        this.tga.SaveAsTga(this.outputStream);
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method           | TestImage           | Mean        | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
    |----------------- |-------------------- |------------:|---------:|---------:|------:|----------:|------------:|
    | 'Magick Tga'     | Tga/targa_24bit.tga | 1,139.82 us | 77.89 us | 4.269 us |  1.00 |    1050 B |        1.00 |
    | 'ImageSharp Tga' | Tga/targa_24bit.tga |    62.50 us | 16.63 us | 0.912 us |  0.05 |     256 B |        0.24 |

    Allocated reports managed memory only.
    */
}
