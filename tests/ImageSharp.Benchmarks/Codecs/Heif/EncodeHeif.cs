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

            // Magick passes these to libheif, which defaults to speed 6, 4:2:0 and SSIM tune; Magick cannot set the
            // tune, so the ImageSharp encoder sets SSIM to match.
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
            Tuning = HeifTuning.Ssim,
            ChromaSubsampling = HeifChromaSubsampling.Yuv420
        });
    }

    /* Results 07.10.2026
     *  BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9550)
        AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
        .NET SDK 11.0.100-preview.7.26381.103
          [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
          Job-UCFAVH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

        Arguments=/p:DebugType=portable  IterationCount=15  LaunchCount=1
        WarmupCount=5

        | Method                   | TestImage      | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0      | Allocated native memory | Native memory leak | Gen1      | Gen2      | Allocated | Alloc Ratio |
        |------------------------- |--------------- |---------:|---------:|---------:|------:|--------:|----------:|------------------------:|-------------------:|----------:|----------:|----------:|------------:|
        | 'Magick Avif'            | Png/Bike.png   | 234.1 ms |  6.26 ms |  5.85 ms |  1.00 |    0.03 |         - |               32,302 KB |               0 KB |         - |         - |  65.55 KB |        1.00 |
        | 'Magick Avif SingleCore' | Png/Bike.png   | 427.6 ms | 11.63 ms |  9.71 ms |  1.83 |    0.06 |         - |               30,607 KB |               0 KB |         - |         - |  71.01 KB |        1.08 |
        | 'ImageSharp Avif'        | Png/Bike.png   | 357.0 ms | 27.36 ms | 22.85 ms |  1.53 |    0.10 |         - |                    1 KB |                  - |         - |         - | 656.82 KB |       10.02 |
        |                          |                |          |          |          |       |         |           |                         |                    |           |           |           |             |
        | 'Magick Avif'            | Png/splash.png | 190.5 ms | 10.31 ms |  9.64 ms |  1.00 |    0.07 |         - |               64,770 KB |               0 KB |         - |         - |   61.6 KB |        1.00 |
        | 'Magick Avif SingleCore' | Png/splash.png | 248.9 ms | 10.20 ms |  9.54 ms |  1.31 |    0.08 |         - |               61,906 KB |               0 KB |         - |         - |  61.98 KB |        1.01 |
        | 'ImageSharp Avif'        | Png/splash.png | 199.8 ms |  9.10 ms |  7.60 ms |  1.05 |    0.06 | 1000.0000 |               23,408 KB |          12,288 KB | 1000.0000 | 1000.0000 | 664.42 KB |       10.79 |

        Run elevated with --iterationCount 15 --warmupCount 5; the three iterations of Config.Short gave unstable means.
        Elevation adds the native memory columns of NativeMemoryProfiler. The Allocated and Alloc Ratio columns count managed memory only.
        The native memory of ImageSharp is its pooled unmanaged buffers; the pool keeps them, so the profiler reports them as a leak.
        Both encoders use quality 75, speed 6, 4:2:0 and SSIM tune. Magick's libheif encodes with one thread per
        logical core; the SingleCore case pins the Magick process to one core. The ImageSharp encoder uses one thread.
     */
}
