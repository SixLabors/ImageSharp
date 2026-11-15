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

    // Both libraries read from these streams. Each call rewinds its stream, so the results measure the decoders only.
    private MemoryStream webpLossyStream;

    private MemoryStream webpLosslessStream;

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
        this.webpLossyStream ??= new MemoryStream(this.webpLossyBytes, false);
        this.webpLosslessStream ??= new MemoryStream(this.webpLosslessBytes, false);
    }

    /// <summary>
    /// Releases the shared input streams.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.webpLossyStream.Dispose();
        this.webpLossyStream = null;
        this.webpLosslessStream.Dispose();
        this.webpLosslessStream = null;
    }

    [Benchmark(Description = "Magick Lossy Webp")]
    public uint WebpLossyMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.WebP };
        this.webpLossyStream.Position = 0;
        using MagickImage image = new(this.webpLossyStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Lossy Webp")]
    public int WebpLossy()
    {
        this.webpLossyStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.webpLossyStream);
        return image.Height;
    }

    [Benchmark(Description = "Magick Lossless Webp")]
    public uint WebpLosslessMagick()
    {
        MagickReadSettings settings = new()
        { Format = MagickFormat.WebP };
        this.webpLosslessStream.Position = 0;
        using MagickImage image = new(this.webpLosslessStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Lossless Webp")]
    public int WebpLossless()
    {
        this.webpLosslessStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.webpLosslessStream);
        return image.Height;
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                     | TestImageLossy        | TestImageLossless        | Mean      | Error     | StdDev   | Gen0      | Gen1      | Gen2      | Allocated  |
    |--------------------------- |---------------------- |------------------------- |----------:|----------:|---------:|----------:|----------:|----------:|-----------:|
    | 'Magick Lossy Webp'        | Webp/earth_lossy.webp | Webp/earth_lossless.webp |  68.13 ms | 10.892 ms | 0.597 ms |         - |         - |         - |    6.23 KB |
    | 'ImageSharp Lossy Webp'    | Webp/earth_lossy.webp | Webp/earth_lossless.webp |  71.58 ms | 21.414 ms | 1.174 ms | 2000.0000 | 2000.0000 | 2000.0000 | 1220.14 KB |
    | 'Magick Lossless Webp'     | Webp/earth_lossy.webp | Webp/earth_lossless.webp | 105.74 ms |  7.433 ms | 0.407 ms |         - |         - |         - |    7.19 KB |
    | 'ImageSharp Lossless Webp' | Webp/earth_lossy.webp | Webp/earth_lossless.webp | 105.98 ms |  4.747 ms | 0.260 ms | 2600.0000 | 2600.0000 | 2600.0000 | 1706.99 KB |

    Allocated reports managed memory only.
    */
}
