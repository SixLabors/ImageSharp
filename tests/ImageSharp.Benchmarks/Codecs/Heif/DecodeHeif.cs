// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Compares AVIF decoding in ImageSharp and Magick.NET.
/// </summary>
[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class DecodeHeif
{
    private byte[] avifBytes;

    // Both libraries read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream inputStream;

    /// <summary>
    /// Gets or sets the AVIF test image.
    /// </summary>
    [Params(TestImages.Heif.IrvineAvif, TestImages.Heif.Av1Deblocking8BitAvif)]
    public string TestImage { get; set; }

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    /// <summary>
    /// Reads the encoded test image.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        this.avifBytes ??= File.ReadAllBytes(this.TestImageFullPath);
        this.inputStream ??= new MemoryStream(this.avifBytes, false);
    }

    /// <summary>
    /// Pins the benchmark process to the first core and reads the encoded test image.
    /// </summary>
    [GlobalSetup(Target = nameof(AvifMagickSingleCore))]
    public void ReadImagesOnSingleCore()
    {
        // Each benchmark runs in its own process, so the pin confines every libheif thread of this case only.
        Process.GetCurrentProcess().ProcessorAffinity = 1;
        this.ReadImages();
    }

    /// <summary>
    /// Releases the shared input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
    }

    /// <summary>
    /// Decodes the image with Magick.NET.
    /// </summary>
    /// <returns>The image width.</returns>
    [Benchmark(Baseline = true, Description = "Magick Avif")]
    public uint AvifMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.Avif };
        this.inputStream.Position = 0;
        using MagickImage image = new(this.inputStream, settings);
        return image.Width;
    }

    /// <summary>
    /// Decodes the image with Magick.NET on a single core.
    /// </summary>
    /// <returns>The image width.</returns>
    [Benchmark(Description = "Magick Avif SingleCore")]
    public uint AvifMagickSingleCore() => this.AvifMagick();

    /// <summary>
    /// Decodes the image with ImageSharp.
    /// </summary>
    /// <returns>The image height.</returns>
    [Benchmark(Description = "ImageSharp Avif")]
    public int Avif()
    {
        this.inputStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.inputStream);
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

    | Method                   | TestImage                    | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
    |------------------------- |----------------------------- |---------:|----------:|---------:|------:|--------:|--------:|--------:|--------:|----------:|------------:|
    | 'Magick Avif'            | Heif/Irvine_CA.avif          | 21.79 ms |  7.909 ms | 0.434 ms |  1.00 |    0.02 |       - |       - |       - |   5.02 KB |        1.00 |
    | 'Magick Avif SingleCore' | Heif/Irvine_CA.avif          | 24.12 ms | 57.377 ms | 3.145 ms |  1.11 |    0.13 |       - |       - |       - |   5.03 KB |        1.00 |
    | 'ImageSharp Avif'        | Heif/Irvine_CA.avif          | 13.25 ms |  2.303 ms | 0.126 ms |  0.61 |    0.01 | 78.1250 | 62.5000 | 46.8750 |  560.9 KB |      111.74 |
    |                          |                              |          |           |          |       |         |         |         |         |           |             |
    | 'Magick Avif'            | Heif/libavif-kodim23-8b.avif | 23.65 ms |  4.813 ms | 0.264 ms |  1.00 |    0.01 |       - |       - |       - |   5.02 KB |        1.00 |
    | 'Magick Avif SingleCore' | Heif/libavif-kodim23-8b.avif | 23.43 ms |  2.761 ms | 0.151 ms |  0.99 |    0.01 |       - |       - |       - |   5.02 KB |        1.00 |
    | 'ImageSharp Avif'        | Heif/libavif-kodim23-8b.avif | 11.59 ms | 10.820 ms | 0.593 ms |  0.49 |    0.02 | 78.1250 | 62.5000 | 46.8750 | 541.18 KB |      107.81 |

    Allocated reports managed memory only.
    The existing SingleCore method pins only its own Magick process. Other methods retain their existing threading behavior.
    */
}
