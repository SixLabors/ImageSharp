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

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-UCFAVH : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=15  LaunchCount=1
    WarmupCount=5

    | Method                    | TestImage       |       Mean |      Error |     StdDev |   Ratio |   RatioSD |        Gen0 |        Gen1 |        Gen2 |    Allocated |   Alloc Ratio |
    | ------------------------- | --------------- | ---------: | ---------: | ---------: | ------: | --------: | ----------: | ----------: | ----------: | -----------: | ------------: |
    | 'Magick Avif'             | Png/Bike.png    |   228.6 ms |    2.37 ms |    2.10 ms |    1.00 |      0.01 |           - |           - |           - |     65.55 KB |          1.00 |
    | 'Magick Avif SingleCore'  | Png/Bike.png    |   426.1 ms |   19.79 ms |   17.54 ms |    1.86 |      0.08 |           - |           - |           - |     71.01 KB |          1.08 |
    | 'ImageSharp Avif'         | Png/Bike.png    |   233.6 ms |   14.79 ms |   13.84 ms |    1.02 |      0.06 |   1000.0000 |   1000.0000 |   1000.0000 |   2118.65 KB |         32.32 |
    |                           |                 |            |            |            |         |           |             |             |             |              |               |
    | 'Magick Avif'             | Png/splash.png  |   188.9 ms |    4.13 ms |    3.45 ms |    1.00 |      0.02 |           - |           - |           - |     60.55 KB |          1.00 |
    | 'Magick Avif SingleCore'  | Png/splash.png  |   233.8 ms |    3.52 ms |    2.94 ms |    1.24 |      0.03 |           - |           - |           - |     61.51 KB |          1.02 |
    | 'ImageSharp Avif'         | Png/splash.png  |   176.3 ms |   19.17 ms |   17.93 ms |    0.93 |      0.09 |   3000.0000 |   3000.0000 |   3000.0000 |   2703.02 KB |         44.64 |

    Allocated reports managed memory only.
    Run with --warmupCount 5 --iterationCount 15. Three measurements gave unstable ImageSharp means.
    Both encoders use quality 75, speed 6 and 4:2:0. The existing SingleCore method pins only its own Magick process.
    */
}
