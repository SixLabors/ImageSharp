// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

            // Magick passes these to libheif, which defaults to speed 6 and 4:2:0; they are set so both encoders match.
            this.avifMagick.Settings.SetDefine(MagickFormat.Heic, "speed", "6");
            this.avifMagick.Settings.SetDefine(MagickFormat.Heic, "chroma", "420");
        }
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

    /* Results 07.10.2026
     *  BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9550)
        AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
        .NET SDK 11.0.100-preview.7.26381.103
          [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
          Job-RCQXAA : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

        Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
        WarmupCount=3

        | Method            | TestImage      | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
        |------------------ |--------------- |---------:|----------:|---------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
        | 'Magick Avif'     | Png/Bike.png   | 184.6 ms |  19.39 ms |  1.06 ms |  1.00 |    0.01 |         - |         - |         - |  61.65 KB |        1.00 |
        | 'ImageSharp Avif' | Png/Bike.png   | 324.2 ms | 514.22 ms | 28.19 ms |  1.76 |    0.13 |         - |         - |         - | 648.82 KB |       10.52 |
        |                   |                |          |           |          |       |         |           |           |           |           |             |
        | 'Magick Avif'     | Png/splash.png | 167.3 ms |  23.69 ms |  1.30 ms |  1.00 |    0.01 |         - |         - |         - |  61.66 KB |        1.00 |
        | 'ImageSharp Avif' | Png/splash.png | 229.2 ms |  91.54 ms |  5.02 ms |  1.37 |    0.03 | 1000.0000 | 1000.0000 | 1000.0000 |  657.2 KB |       10.66 |

        Both encoders use quality 75, speed 6 and 4:2:0. Magick's libheif encodes with one thread per logical core;
        Magick.NET has no setting to change that. The ImageSharp encoder uses one thread.
     */
}
