// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

using ImageMagick;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class DecodeWebp
{
    private Configuration configuration;

    private byte[] webpLossyBytes;

    private byte[] webpLosslessBytes;

    private string TestImageLossyFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImageLossy);

    private string TestImageLosslessFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImageLossless);

    [Params(TestImages.Webp.Lossy.Earth)]
    public string TestImageLossy { get; set; }

    [Params(TestImages.Webp.Lossless.Earth)]
    public string TestImageLossless { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        this.configuration = Configuration.CreateDefaultInstance();
        new WebpConfigurationModule().Configure(this.configuration);

        this.webpLossyBytes ??= File.ReadAllBytes(this.TestImageLossyFullPath);
        this.webpLosslessBytes ??= File.ReadAllBytes(this.TestImageLosslessFullPath);
    }

    [Benchmark(Description = "Magick Lossy Webp")]
    public uint WebpLossyMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.WebP };
        using MemoryStream memoryStream = new(this.webpLossyBytes);
        using MagickImage image = new(memoryStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Lossy Webp")]
    public int WebpLossy()
    {
        using MemoryStream memoryStream = new(this.webpLossyBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return image.Height;
    }

    [Benchmark(Description = "Magick Lossless Webp")]
    public uint WebpLosslessMagick()
    {
        MagickReadSettings settings = new()
        { Format = MagickFormat.WebP };
        using MemoryStream memoryStream = new(this.webpLosslessBytes);
        using MagickImage image = new(memoryStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Lossless Webp")]
    public int WebpLossless()
    {
        using MemoryStream memoryStream = new(this.webpLosslessBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return image.Height;
    }

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                      | TestImageLossy         | TestImageLossless         |        Mean |      Error |     StdDev |        Gen0 |        Gen1 |        Gen2 |    Allocated |
    | --------------------------- | ---------------------- | ------------------------- | ----------: | ---------: | ---------: | ----------: | ----------: | ----------: | -----------: |
    | 'Magick Lossy Webp'         | Webp/earth_lossy.webp  | Webp/earth_lossless.webp  |    65.04 ms |   2.051 ms |   0.112 ms |           - |           - |           - |      6.26 KB |
    | 'ImageSharp Lossy Webp'     | Webp/earth_lossy.webp  | Webp/earth_lossless.webp  |    69.88 ms |   2.704 ms |   0.148 ms |   2000.0000 |   2000.0000 |   2000.0000 |   1134.94 KB |
    | 'Magick Lossless Webp'      | Webp/earth_lossy.webp  | Webp/earth_lossless.webp  |   104.51 ms |   8.797 ms |   0.482 ms |           - |           - |           - |      7.21 KB |
    | 'ImageSharp Lossless Webp'  | Webp/earth_lossy.webp  | Webp/earth_lossless.webp  |   104.13 ms |   7.850 ms |   0.430 ms |   2600.0000 |   2600.0000 |   2600.0000 |   1673.37 KB |

    Allocated reports managed memory only.
    */
}
