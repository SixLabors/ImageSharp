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
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.tga.Dispose();
        this.tga = null;
        this.tgaMagick.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Magick Tga")]
    public void MagickTga()
    {
        using MemoryStream memoryStream = new();
        this.tgaMagick.Write(memoryStream, MagickFormat.Tga);
    }

    [Benchmark(Description = "ImageSharp Tga")]
    public void ImageSharpTga()
    {
        using MemoryStream memoryStream = new();
        this.tga.SaveAsTga(memoryStream);
    }

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method            | TestImage            |          Mean |      Error |     StdDev |   Ratio |      Gen0 |      Gen1 |      Gen2 |   Allocated |   Alloc Ratio |
    | ----------------- | -------------------- | ------------: | ---------: | ---------: | ------: | --------: | --------: | --------: | ----------: | ------------: |
    | 'Magick Tga'      | Tga/targa_24bit.tga  |   1,163.39 μs |   10.06 μs |   0.552 μs |    1.00 |   41.0156 |   41.0156 |   41.0156 |   257.63 KB |          1.00 |
    | 'ImageSharp Tga'  | Tga/targa_24bit.tga  |      77.12 μs |   12.16 μs |   0.667 μs |    0.07 |    7.8125 |    1.4648 |         - |   128.27 KB |          0.50 |

    Allocated reports managed memory only.
    */
}
