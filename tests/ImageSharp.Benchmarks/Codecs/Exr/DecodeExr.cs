// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

using ImageMagick;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class DecodeExr
{
    private Configuration configuration;

    private byte[] imageBytes;

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Exr.Benchmark)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        this.configuration = Configuration.CreateDefaultInstance();
        new ExrConfigurationModule().Configure(this.configuration);

        this.imageBytes ??= File.ReadAllBytes(this.TestImageFullPath);
    }

    [Benchmark(Description = "Magick Exr")]
    public uint ExrImageMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.Exr };
        using MemoryStream memoryStream = new(this.imageBytes);
        using MagickImage image = new(memoryStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Exr")]
    public int ExrImageSharp()
    {
        using MemoryStream memoryStream = new(this.imageBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return image.Height;
    }

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method            | TestImage                     |       Mean |      Error |     StdDev |      Gen0 |      Gen1 |      Gen2 |   Allocated |
    | ----------------- | ----------------------------- | ---------: | ---------: | ---------: | --------: | --------: | --------: | ----------: |
    | 'Magick Exr'      | Exr/Calliphora_benchmark.exr  |   19.17 ms |   1.278 ms |   0.070 ms |         - |         - |         - |     5.07 KB |
    | 'ImageSharp Exr'  | Exr/Calliphora_benchmark.exr  |   25.71 ms |   1.120 ms |   0.061 ms |   31.2500 |   31.2500 |   31.2500 |    40.01 KB |

    Allocated reports managed memory only.
    ImageSharp writes Rgba32 output; Magick uses Q16 samples. This comparison does not measure HDR output.
    */
}
