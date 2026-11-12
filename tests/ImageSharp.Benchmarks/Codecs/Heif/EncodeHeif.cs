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

    // Both libraries write into this stream. Each call rewinds it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    private MemoryStream outputStream;

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
            this.outputStream = new MemoryStream();
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
    /// Releases the source images and the shared output stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.avif.Dispose();
        this.avifMagick.Dispose();
        this.outputStream.Dispose();
    }

    /// <summary>
    /// Encodes the image with Magick.NET.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Magick Avif")]
    public void MagickAvif()
    {
        this.outputStream.Position = 0;
        this.avifMagick.Quality = 75;
        this.avifMagick.Write(this.outputStream, MagickFormat.Avif);
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
        this.outputStream.Position = 0;
        this.avif.Save(this.outputStream, new HeifEncoder
        {
            Quality = 75,
            Speed = HeifEncodingSpeed.Level6,
            ChromaSubsampling = HeifChromaSubsampling.Yuv420
        });
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-UCFAVH : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=15  LaunchCount=1
    WarmupCount=5

    | Method                   | TestImage      | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
    |------------------------- |--------------- |---------:|---------:|---------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
    | 'Magick Avif'            | Png/Bike.png   | 223.8 ms | 18.78 ms | 17.57 ms |  1.01 |    0.12 |         - |         - |         - |   9.47 KB |        1.00 |
    | 'Magick Avif SingleCore' | Png/Bike.png   | 408.3 ms |  3.93 ms |  3.28 ms |  1.84 |    0.16 |         - |         - |         - |  15.12 KB |        1.60 |
    | 'ImageSharp Avif'        | Png/Bike.png   | 222.9 ms |  1.89 ms |  1.68 ms |  1.00 |    0.09 | 3000.0000 | 3000.0000 | 3000.0000 | 2062.4 KB |      217.74 |
    |                          |                |          |          |          |       |         |           |           |           |           |             |
    | 'Magick Avif'            | Png/splash.png | 159.5 ms |  1.71 ms |  1.51 ms |  1.00 |    0.01 |         - |         - |         - |   7.41 KB |        1.00 |
    | 'Magick Avif SingleCore' | Png/splash.png | 238.5 ms |  4.45 ms |  4.16 ms |  1.50 |    0.03 |         - |         - |         - |   5.86 KB |        0.79 |
    | 'ImageSharp Avif'        | Png/splash.png | 146.0 ms |  2.07 ms |  1.73 ms |  0.92 |    0.01 | 3000.0000 | 3000.0000 | 3000.0000 | 2647.5 KB |      357.19 |

    Allocated reports managed memory only.
    Run with --warmupCount 5 --iterationCount 15. Three measurements gave unstable ImageSharp means.
    Both encoders use quality 75, speed 6 and 4:2:0. The existing SingleCore method pins only its own Magick process.
    */
}
