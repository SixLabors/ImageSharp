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
public class EncodeWebp
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
            this.webpMagick = new MagickImage(this.TestImageFullPath);
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

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                      | TestImage     |        Mean |       Error |     StdDev |   Ratio |        Gen0 |        Gen1 |        Gen2 |    Allocated |   Alloc Ratio |
    | --------------------------- | ------------- | ----------: | ----------: | ---------: | ------: | ----------: | ----------: | ----------: | -----------: | ------------: |
    | 'Magick Webp Lossy'         | Png/Bike.png  |    13.60 ms |    1.243 ms |   0.068 ms |    0.08 |           - |           - |           - |     58.99 KB |          0.12 |
    | 'ImageSharp Webp Lossy'     | Png/Bike.png  |    40.14 ms |    3.336 ms |   0.183 ms |    0.25 |           - |           - |           - |    259.71 KB |          0.51 |
    | 'Magick Webp Lossless'      | Png/Bike.png  |   162.98 ms |    4.846 ms |   0.266 ms |    1.00 |           - |           - |           - |    508.96 KB |          1.00 |
    | 'ImageSharp Webp Lossless'  | Png/Bike.png  |   102.26 ms |   46.199 ms |   2.532 ms |    0.63 |   2000.0000 |   2000.0000 |   2000.0000 |   3898.44 KB |          7.66 |

    Allocated reports managed memory only.
    Ratio uses Magick Webp Lossless as the baseline for every method, including lossy encoding.
    */
}
