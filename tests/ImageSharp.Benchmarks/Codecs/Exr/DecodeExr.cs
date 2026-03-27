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

    [Params(TestImages.Exr.Benchamrk)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        this.configuration = Configuration.CreateDefaultInstance();
        new ExrConfigurationModule().Configure(this.configuration);

        this.imageBytes ??= File.ReadAllBytes(this.TestImageFullPath);
    }

    [Benchmark(Description = "Magick Exr")]
    public int ExrImageMagick()
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

    /* Results 27.03.2026
        BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
        Intel Core i7-14700T 1.30GHz, 1 CPU, 28 logical and 20 physical cores
        .NET SDK 10.0.201
          [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v3
          Job-VDWIGO : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v3

        Runtime=.NET 8.0  Arguments=/p:DebugType=portable  IterationCount=3
        LaunchCount=1  WarmupCount=3

        | Method           | TestImage                    | Mean     | Error    | StdDev   | Allocated |
        |----------------- |----------------------------- |---------:|---------:|---------:|----------:|
        | 'Magick Exr'     | Exr/Calliphora_benchmark.exr | 20.37 ms | 0.790 ms | 0.043 ms |  12.98 KB |
        | 'ImageSharp Exr' | Exr/Calliphora_benchmark.exr | 45.68 ms | 4.999 ms | 0.274 ms |  34.09 KB |
     */
}
