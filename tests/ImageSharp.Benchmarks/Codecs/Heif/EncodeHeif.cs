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
        this.avif.Save(memoryStream, new HeifEncoder { Quality = 75 });
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
        | 'Magick Avif'     | Png/Bike.png   | 231.4 ms |  42.43 ms |  2.33 ms |  1.00 |    0.01 |         - |         - |         - |  61.41 KB |        1.00 |
        | 'ImageSharp Avif' | Png/Bike.png   | 428.6 ms | 524.54 ms | 28.75 ms |  1.85 |    0.11 |         - |         - |         - | 715.71 KB |       11.65 |
        |                   |                |          |           |          |       |         |           |           |           |           |             |
        | 'Magick Avif'     | Png/splash.png | 187.5 ms |  43.23 ms |  2.37 ms |  1.00 |    0.02 |         - |         - |         - |  60.31 KB |        1.00 |
        | 'ImageSharp Avif' | Png/splash.png | 261.4 ms | 356.87 ms | 19.56 ms |  1.39 |    0.09 | 1000.0000 | 1000.0000 | 1000.0000 |  657.2 KB |       10.90 |
     */
}
