// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
          Job-RCQXAA : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

        Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
        WarmupCount=3

        | Method            | TestImage                    | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
        |------------------ |----------------------------- |---------:|----------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
        | 'Magick Avif'     | Heif/Irvine_CA.avif          | 20.97 ms | 15.277 ms | 0.837 ms |  1.00 |    0.05 |       - |       - |   5.07 KB |        1.00 |
        | 'ImageSharp Avif' | Heif/Irvine_CA.avif          | 14.78 ms |  1.847 ms | 0.101 ms |  0.71 |    0.02 | 31.2500 |       - | 537.38 KB |      105.90 |
        |                   |                              |          |           |          |       |         |         |         |           |             |
        | 'Magick Avif'     | Heif/libavif-kodim23-8b.avif | 22.76 ms |  3.266 ms | 0.179 ms |  1.00 |    0.01 |       - |       - |   5.07 KB |        1.00 |
        | 'ImageSharp Avif' | Heif/libavif-kodim23-8b.avif | 13.19 ms |  1.723 ms | 0.094 ms |  0.58 |    0.01 | 31.2500 | 15.6250 | 533.23 KB |      105.09 |
     */
}
