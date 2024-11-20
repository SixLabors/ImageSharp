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
    public int WebpLossyMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.WebP };
        using MemoryStream memoryStream = new(this.webpLossyBytes);
        using MagickImage image = new(memoryStream, settings);
        return (int)image.Width;
    }

    [Benchmark(Description = "ImageSharp Lossy Webp")]
    public int WebpLossy()
    {
        using MemoryStream memoryStream = new(this.webpLossyBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return image.Height;
    }

    [Benchmark(Description = "Magick Lossless Webp")]
    public int WebpLosslessMagick()
    {
        MagickReadSettings settings = new()
        { Format = MagickFormat.WebP };
        using MemoryStream memoryStream = new(this.webpLossyBytes);
        using MagickImage image = new(memoryStream, settings);
        return (int)image.Width;
    }

    [Benchmark(Description = "ImageSharp Lossless Webp")]
    public int WebpLossless()
    {
        using MemoryStream memoryStream = new(this.webpLosslessBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return image.Height;
    }

    /* Results 04.11.2021
     *  BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19043.1320 (21H1/May2021Update)
        Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
        .NET SDK=6.0.100-rc.2.21505.57
          [Host]     : .NET 5.0.11 (5.0.1121.47308), X64 RyuJIT
          Job-WQLXJO : .NET 5.0.11 (5.0.1121.47308), X64 RyuJIT
          Job-OJJAMD : .NET Core 3.1.20 (CoreCLR 4.700.21.47003, CoreFX 4.700.21.47101), X64 RyuJIT
          Job-OMFOAS : .NET Framework 4.8 (4.8.4420.0), X64 RyuJIT

        |                     Method |        Job |              Runtime |             Arguments |        TestImageLossy |        TestImageLossless |       Mean |     Error |  StdDev |    Gen 0 | Gen 1 | Gen 2 | Allocated |
        |--------------------------- |----------- |--------------------- |---------------------- |---------------------- |------------------------- |-----------:|----------:|--------:|---------:|------:|------:|----------:|
        |        'Magick Lossy Webp' | Job-HLWZLL |             .NET 5.0 | /p:DebugType=portable | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   107.9 ms |  28.91 ms | 1.58 ms |        - |     - |     - |     25 KB |
        |    'ImageSharp Lossy Webp' | Job-HLWZLL |             .NET 5.0 | /p:DebugType=portable | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   282.3 ms |  25.40 ms | 1.39 ms | 500.0000 |     - |     - |  2,428 KB |
        |     'Magick Lossless Webp' | Job-HLWZLL |             .NET 5.0 | /p:DebugType=portable | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   106.3 ms |  11.99 ms | 0.66 ms |        - |     - |     - |     16 KB |
        | 'ImageSharp Lossless Webp' | Job-HLWZLL |             .NET 5.0 | /p:DebugType=portable | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   280.2 ms |   6.21 ms | 0.34 ms |        - |     - |     - |  2,092 KB |
        |        'Magick Lossy Webp' | Job-ALQPDS |        .NET Core 3.1 |               Default | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   106.2 ms |   9.32 ms | 0.51 ms |        - |     - |     - |     15 KB |
        |    'ImageSharp Lossy Webp' | Job-ALQPDS |        .NET Core 3.1 |               Default | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   295.8 ms |  21.25 ms | 1.16 ms | 500.0000 |     - |     - |  2,427 KB |
        |     'Magick Lossless Webp' | Job-ALQPDS |        .NET Core 3.1 |               Default | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   106.5 ms |   4.07 ms | 0.22 ms |        - |     - |     - |     15 KB |
        | 'ImageSharp Lossless Webp' | Job-ALQPDS |        .NET Core 3.1 |               Default | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   464.0 ms |  55.70 ms | 3.05 ms |        - |     - |     - |  2,090 KB |
        |        'Magick Lossy Webp' | Job-RYVVNN | .NET Framework 4.7.2 |               Default | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   108.0 ms |  29.60 ms | 1.62 ms |        - |     - |     - |     32 KB |
        |    'ImageSharp Lossy Webp' | Job-RYVVNN | .NET Framework 4.7.2 |               Default | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   564.9 ms |  29.69 ms | 1.63 ms |        - |     - |     - |  2,436 KB |
        |     'Magick Lossless Webp' | Job-RYVVNN | .NET Framework 4.7.2 |               Default | Webp/earth_lossy.webp | Webp/earth_lossless.webp |   106.2 ms |   4.74 ms | 0.26 ms |        - |     - |     - |     18 KB |
        | 'ImageSharp Lossless Webp' | Job-RYVVNN | .NET Framework 4.7.2 |               Default | Webp/earth_lossy.webp | Webp/earth_lossless.webp | 1,767.5 ms | 106.33 ms | 5.83 ms |        - |     - |     - |  9,729 KB |
     */
}
