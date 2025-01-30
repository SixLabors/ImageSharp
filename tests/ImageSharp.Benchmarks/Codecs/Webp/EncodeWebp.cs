// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using ImageMagick;
using ImageMagick.Formats;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public class EncodeWebp
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private MagickImage webpMagick;
    private Image<Rgba32> webp;

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Png.Bike)] // The bike image will have all 3 transforms as lossless webp.
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.webp == null)
        {
            this.webp = Image.Load<Rgba32>(this.TestImageFullPath);
            this.webpMagick = new(this.TestImageFullPath);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.webp.Dispose();
        this.webpMagick.Dispose();
    }

    [Benchmark(Description = "Magick Webp Lossy")]
    public void MagickWebpLossy()
    {
        using MemoryStream memoryStream = new();

        WebPWriteDefines defines = new()
        {
            Lossless = false,
            Method = 4,
            AlphaCompression = WebPAlphaCompression.None,
            FilterStrength = 60,
            SnsStrength = 50,
            Pass = 1
        };

        this.webpMagick.Quality = 75;
        this.webpMagick.Write(memoryStream, defines);
    }

    [Benchmark(Description = "ImageSharp Webp Lossy")]
    public void ImageSharpWebpLossy()
    {
        using MemoryStream memoryStream = new();
        this.webp.Save(memoryStream, new WebpEncoder()
        {
            FileFormat = WebpFileFormatType.Lossy,
            Method = WebpEncodingMethod.Level4,
            UseAlphaCompression = false,
            FilterStrength = 60,
            SpatialNoiseShaping = 50,
            EntropyPasses = 1
        });
    }

    [Benchmark(Baseline = true, Description = "Magick Webp Lossless")]
    public void MagickWebpLossless()
    {
        using MemoryStream memoryStream = new();
        WebPWriteDefines defines = new()
        {
            Lossless = true,
            Method = 4,
        };

        this.webpMagick.Quality = 75;
        this.webpMagick.Write(memoryStream, defines);
    }

    [Benchmark(Description = "ImageSharp Webp Lossless")]
    public void ImageSharpWebpLossless()
    {
        using MemoryStream memoryStream = new();
        this.webp.Save(memoryStream, new WebpEncoder()
        {
            FileFormat = WebpFileFormatType.Lossless,
            Method = WebpEncodingMethod.Level4,
            NearLossless = false,
            Quality = 75,

            // This is equal to exact = false in libwebp, which is the default.
            TransparentColorMode = TransparentColorMode.Clear
        });
    }

    /* Results 04.11.2021
     * Summary *
    BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19043.1320 (21H1/May2021Update)
    Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
    .NET SDK=6.0.100-rc.2.21505.57
      [Host]     : .NET 5.0.11 (5.0.1121.47308), X64 RyuJIT
      Job-WQLXJO : .NET 5.0.11 (5.0.1121.47308), X64 RyuJIT
      Job-OJJAMD : .NET Core 3.1.20 (CoreCLR 4.700.21.47003, CoreFX 4.700.21.47101), X64 RyuJIT
      Job-OMFOAS : .NET Framework 4.8 (4.8.4420.0), X64 RyuJIT

    IterationCount=3  LaunchCount=1  WarmupCount=3

    |                     Method |        Job |              Runtime |             Arguments |    TestImage |      Mean |     Error |   StdDev | Ratio | RatioSD |       Gen 0 |     Gen 1 |     Gen 2 |  Allocated |
    |--------------------------- |----------- |--------------------- |---------------------- |------------- |----------:|----------:|---------:|------:|--------:|------------:|----------:|----------:|-----------:|
    |        'Magick Webp Lossy' | Job-WQLXJO |             .NET 5.0 | /p:DebugType=portable | Png/Bike.png |  23.33 ms |  1.491 ms | 0.082 ms |  0.15 |    0.00 |           - |         - |         - |      67 KB |
    |    'ImageSharp Webp Lossy' | Job-WQLXJO |             .NET 5.0 | /p:DebugType=portable | Png/Bike.png | 245.80 ms | 24.288 ms | 1.331 ms |  1.53 |    0.01 | 135000.0000 |         - |         - | 552,713 KB |
    |     'Magick Webp Lossless' | Job-WQLXJO |             .NET 5.0 | /p:DebugType=portable | Png/Bike.png | 160.36 ms | 11.131 ms | 0.610 ms |  1.00 |    0.00 |           - |         - |         - |     518 KB |
    | 'ImageSharp Webp Lossless' | Job-WQLXJO |             .NET 5.0 | /p:DebugType=portable | Png/Bike.png | 313.93 ms | 45.605 ms | 2.500 ms |  1.96 |    0.01 |  34000.0000 | 5000.0000 | 2000.0000 | 161,670 KB |
    |                            |            |                      |                       |              |           |           |          |       |         |             |           |           |            |
    |        'Magick Webp Lossy' | Job-OJJAMD |        .NET Core 3.1 |               Default | Png/Bike.png |  23.36 ms |  2.289 ms | 0.125 ms |  0.15 |    0.00 |           - |         - |         - |      67 KB |
    |    'ImageSharp Webp Lossy' | Job-OJJAMD |        .NET Core 3.1 |               Default | Png/Bike.png | 254.64 ms | 19.620 ms | 1.075 ms |  1.59 |    0.00 | 135000.0000 |         - |         - | 552,713 KB |
    |     'Magick Webp Lossless' | Job-OJJAMD |        .NET Core 3.1 |               Default | Png/Bike.png | 160.30 ms |  9.549 ms | 0.523 ms |  1.00 |    0.00 |           - |         - |         - |     518 KB |
    | 'ImageSharp Webp Lossless' | Job-OJJAMD |        .NET Core 3.1 |               Default | Png/Bike.png | 320.35 ms | 22.924 ms | 1.257 ms |  2.00 |    0.01 |  34000.0000 | 5000.0000 | 2000.0000 | 161,669 KB |
    |                            |            |                      |                       |              |           |           |          |       |         |             |           |           |            |
    |        'Magick Webp Lossy' | Job-OMFOAS | .NET Framework 4.7.2 |               Default | Png/Bike.png |  23.37 ms |  0.908 ms | 0.050 ms |  0.15 |    0.00 |           - |         - |         - |      68 KB |
    |    'ImageSharp Webp Lossy' | Job-OMFOAS | .NET Framework 4.7.2 |               Default | Png/Bike.png | 378.67 ms | 25.540 ms | 1.400 ms |  2.36 |    0.01 | 135000.0000 |         - |         - | 554,351 KB |
    |     'Magick Webp Lossless' | Job-OMFOAS | .NET Framework 4.7.2 |               Default | Png/Bike.png | 160.13 ms |  5.115 ms | 0.280 ms |  1.00 |    0.00 |           - |         - |         - |     520 KB |
    | 'ImageSharp Webp Lossless' | Job-OMFOAS | .NET Framework 4.7.2 |               Default | Png/Bike.png | 379.01 ms | 71.192 ms | 3.902 ms |  2.37 |    0.02 |  34000.0000 | 5000.0000 | 2000.0000 | 162,119 KB |
    */
}
