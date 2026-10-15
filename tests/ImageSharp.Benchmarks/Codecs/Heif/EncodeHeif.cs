// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Compares AVIF encoding in ImageSharp and Magick.NET.
/// </summary>
[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class EncodeHeif
{
    private MagickImage avifMagick;
    private Image<Rgba32> avif;

    /// <summary>
    /// Gets or sets the source test image.
    /// </summary>
    [Params(TestImages.Png.Bike, TestImages.Png.Splash)]
    public string TestImage { get; set; }

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    /// <summary>
    /// Reads the source image for both encoders.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        if (this.avif == null)
        {
            this.avif = Image.Load<Rgba32>(this.TestImageFullPath);
            this.avifMagick = new MagickImage(this.TestImageFullPath);

            // Magick passes these to libheif. Its streams use the image quality tune of libaom for color stills, which
            // is also the default of the ImageSharp encoder, so neither encoder sets a tune.
            this.avifMagick.Settings.SetDefine(MagickFormat.Heic, "speed", "6");
            this.avifMagick.Settings.SetDefine(MagickFormat.Heic, "chroma", "420");
        }
    }

    /// <summary>
    /// Pins the benchmark process to the first core and reads the source image for both encoders.
    /// </summary>
    [GlobalSetup(Target = nameof(MagickAvifSingleCore))]
    public void ReadImagesOnSingleCore()
    {
        // Each benchmark runs in its own process, so the pin confines every libheif thread of this case only.
        Process.GetCurrentProcess().ProcessorAffinity = 1;
        this.ReadImages();
    }

    /// <summary>
    /// Releases the source images.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.avif.Dispose();
        this.avifMagick.Dispose();
    }

    /// <summary>
    /// Encodes the image with Magick.NET.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Magick Avif")]
    public void MagickAvif()
    {
        using MemoryStream memoryStream = new();
        this.avifMagick.Quality = 75;
        this.avifMagick.Write(memoryStream, MagickFormat.Avif);
    }

    /// <summary>
    /// Encodes the image with Magick.NET on a single core.
    /// </summary>
    [Benchmark(Description = "Magick Avif SingleCore")]
    public void MagickAvifSingleCore() => this.MagickAvif();

    /// <summary>
    /// Encodes the image with ImageSharp.
    /// </summary>
    [Benchmark(Description = "ImageSharp Avif")]
    public void ImageSharpAvif()
    {
        using MemoryStream memoryStream = new();
        this.avif.Save(memoryStream, new HeifEncoder
        {
            Quality = 75,
            Speed = HeifEncodingSpeed.Level6,
            ChromaSubsampling = HeifChromaSubsampling.Yuv420
        });
    }

    /* Results 09.10.2026
     *  BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9550)
        AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
        .NET SDK 11.0.100-preview.7.26381.103
          [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
          Job-UCFAVH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

        Arguments=/p:DebugType=portable  IterationCount=15  LaunchCount=1
        WarmupCount=5

        | Method                   | TestImage      | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
        |------------------------- |--------------- |---------:|---------:|---------:|------:|--------:|----------:|------------:|
        | 'Magick Avif'            | Png/Bike.png   | 231.6 ms |  2.43 ms |  2.27 ms |  1.00 |    0.01 |  65.55 KB |        1.00 |
        | 'Magick Avif SingleCore' | Png/Bike.png   | 424.8 ms |  7.86 ms |  7.35 ms |  1.83 |    0.04 |  71.01 KB |        1.08 |
        | 'ImageSharp Avif'        | Png/Bike.png   | 241.3 ms |  6.37 ms |  5.65 ms |  1.04 |    0.03 | 362.84 KB |        5.54 |
        |                          |                |          |          |          |       |         |           |             |
        | 'Magick Avif'            | Png/splash.png | 184.7 ms |  3.85 ms |  3.60 ms |  1.00 |    0.03 |  60.55 KB |        1.00 |
        | 'Magick Avif SingleCore' | Png/splash.png | 250.3 ms |  3.36 ms |  2.81 ms |  1.36 |    0.03 |  62.01 KB |        1.02 |
        | 'ImageSharp Avif'        | Png/splash.png | 167.3 ms | 23.64 ms | 22.11 ms |  0.91 |    0.12 | 648.75 KB |       10.71 |

        Run with --iterationCount 15 --warmupCount 5; the three iterations of Config.Short gave unstable means.
        The Allocated and Alloc Ratio columns count managed memory only. An elevated run adds the native memory columns of
        NativeMemoryProfiler: on 07.10.2026 Magick allocated 30 to 65 MB of native memory per encode.
        Both encoders use quality 75, speed 6, 4:2:0 and the image quality tune, and produce equal stream headers.
        Magick's libheif encodes with one thread per logical core; the SingleCore case pins the Magick process to one core.
        The ImageSharp encoder uses one thread.
     */
}
