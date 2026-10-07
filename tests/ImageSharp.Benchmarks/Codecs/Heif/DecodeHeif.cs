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
    public void ReadImages() => this.avifBytes ??= File.ReadAllBytes(this.TestImageFullPath);

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
    /// Decodes the image with Magick.NET.
    /// </summary>
    /// <returns>The image width.</returns>
    [Benchmark(Baseline = true, Description = "Magick Avif")]
    public uint AvifMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.Avif };
        using MemoryStream memoryStream = new(this.avifBytes);
        using MagickImage image = new(memoryStream, settings);
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
        using MemoryStream memoryStream = new(this.avifBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return image.Height;
    }

    /* Results 07.10.2026
     *  BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9550)
        AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
        .NET SDK 11.0.100-preview.7.26381.103
          [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
          Job-UCFAVH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

        Arguments=/p:DebugType=portable  IterationCount=15  LaunchCount=1
        WarmupCount=5

        | Method                   | TestImage                    | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0    | Allocated native memory | Native memory leak | Gen1    | Allocated | Alloc Ratio |
        |------------------------- |----------------------------- |---------:|---------:|---------:|------:|--------:|--------:|------------------------:|-------------------:|--------:|----------:|------------:|
        | 'Magick Avif'            | Heif/Irvine_CA.avif          | 19.94 ms | 0.249 ms | 0.221 ms |  1.00 |    0.02 |       - |               13,408 KB |               0 KB |       - |   5.07 KB |        1.00 |
        | 'Magick Avif SingleCore' | Heif/Irvine_CA.avif          | 20.51 ms | 0.531 ms | 0.471 ms |  1.03 |    0.03 |       - |               13,409 KB |               0 KB |       - |   5.08 KB |        1.00 |
        | 'ImageSharp Avif'        | Heif/Irvine_CA.avif          | 14.30 ms | 0.127 ms | 0.119 ms |  0.72 |    0.01 | 31.2500 |                    0 KB |                  - |       - | 537.38 KB |      105.90 |
        |                          |                              |          |          |          |       |         |         |                         |                    |         |           |             |
        | 'Magick Avif'            | Heif/libavif-kodim23-8b.avif | 24.35 ms | 2.448 ms | 2.290 ms |  1.01 |    0.12 |       - |               12,420 KB |               0 KB |       - |   5.07 KB |        1.00 |
        | 'Magick Avif SingleCore' | Heif/libavif-kodim23-8b.avif | 22.56 ms | 0.436 ms | 0.364 ms |  0.93 |    0.08 |       - |               12,421 KB |               0 KB |       - |   5.08 KB |        1.00 |
        | 'ImageSharp Avif'        | Heif/libavif-kodim23-8b.avif | 13.42 ms | 0.239 ms | 0.211 ms |  0.56 |    0.05 | 31.2500 |                1,639 KB |           1,638 KB | 15.6250 | 533.23 KB |      105.09 |

        Run elevated with --iterationCount 15 --warmupCount 5; the three iterations of Config.Short gave unstable means.
        Elevation adds the native memory columns of NativeMemoryProfiler. The Allocated and Alloc Ratio columns count managed memory only.
        The native memory of ImageSharp is its pooled unmanaged buffers; the pool keeps them, so the profiler reports them as a leak.
     */
}
