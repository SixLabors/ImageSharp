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

    // Both libraries write into this stream. Each call rewinds it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    private MemoryStream outputStream;

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
            this.outputStream = new MemoryStream();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.webp.Dispose();
        this.webpMagick.Dispose();
        this.outputStream.Dispose();
    }

    [Benchmark(Description = "Magick Webp Lossy")]
    public void MagickWebpLossy()
    {
        this.outputStream.Position = 0;

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
        this.webpMagick.Write(this.outputStream, defines);
    }

    [Benchmark(Description = "ImageSharp Webp Lossy")]
    public void ImageSharpWebpLossy()
    {
        this.outputStream.Position = 0;
        this.webp.Save(this.outputStream, new WebpEncoder()
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
        this.outputStream.Position = 0;
        WebPWriteDefines defines = new()
        {
            Lossless = true,
            Method = 4,
        };

        this.webpMagick.Quality = 75;
        this.webpMagick.Write(this.outputStream, defines);
    }

    [Benchmark(Description = "ImageSharp Webp Lossless")]
    public void ImageSharpWebpLossless()
    {
        this.outputStream.Position = 0;
        this.webp.Save(this.outputStream, new WebpEncoder()
        {
            FileFormat = WebpFileFormatType.Lossless,
            Method = WebpEncodingMethod.Level4,
            NearLossless = false,
            Quality = 75,

            // This is equal to exact = false in libwebp, which is the default.
            TransparentColorMode = TransparentColorMode.Clear
        });
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                     | TestImage    | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated  | Alloc Ratio |
    |--------------------------- |------------- |----------:|----------:|---------:|------:|--------:|----------:|----------:|----------:|-----------:|------------:|
    | 'Magick Webp Lossy'        | Png/Bike.png |  13.87 ms |  2.173 ms | 0.119 ms |  0.08 |    0.00 |         - |         - |         - |    2.83 KB |        0.59 |
    | 'ImageSharp Webp Lossy'    | Png/Bike.png |  41.50 ms |  3.320 ms | 0.182 ms |  0.25 |    0.00 |         - |         - |         - |  285.93 KB |       59.41 |
    | 'Magick Webp Lossless'     | Png/Bike.png | 166.12 ms | 23.937 ms | 1.312 ms |  1.00 |    0.01 |         - |         - |         - |    4.81 KB |        1.00 |
    | 'ImageSharp Webp Lossless' | Png/Bike.png | 105.27 ms | 59.800 ms | 3.278 ms |  0.63 |    0.02 | 2200.0000 | 2200.0000 | 2200.0000 | 4815.18 KB |    1,000.56 |

    Allocated reports managed memory only.
    Ratio uses Magick Webp Lossless as the baseline for every method, including lossy encoding.
    */
}
