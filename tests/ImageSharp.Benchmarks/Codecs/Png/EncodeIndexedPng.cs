// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Benchmarks saving png files using different quantizers.
/// System.Drawing cannot save indexed png files so we cannot compare.
/// </summary>
[Config(typeof(Config.Short))]
public class EncodeIndexedPng
{
    // System.Drawing needs this.
    private FileStream bmpStream;
    private Image<Rgba32> bmpCore;

    // All methods write into this stream. Each call rewinds it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    private MemoryStream outputStream;

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Bmp.Car));
            this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
            this.bmpStream.Position = 0;
            this.outputStream = new MemoryStream();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpStream.Dispose();
        this.bmpStream = null;
        this.bmpCore.Dispose();
        this.outputStream.Dispose();
    }

    [Benchmark(Baseline = true, Description = "ImageSharp Hexadecatree Png")]
    public void PngCoreHexadecatree()
    {
        this.outputStream.Position = 0;
        PngEncoder options = new() { Quantizer = KnownQuantizers.Hexadecatree };
        this.bmpCore.SaveAsPng(this.outputStream, options);
    }

    [Benchmark(Description = "ImageSharp Hexadecatree NoDither Png")]
    public void PngCoreHexadecatreeNoDither()
    {
        this.outputStream.Position = 0;
        PngEncoder options = new() { Quantizer = new HexadecatreeQuantizer(new QuantizerOptions { Dither = null }) };
        this.bmpCore.SaveAsPng(this.outputStream, options);
    }

    [Benchmark(Description = "ImageSharp Palette Png")]
    public void PngCorePalette()
    {
        this.outputStream.Position = 0;
        PngEncoder options = new() { Quantizer = KnownQuantizers.WebSafe };
        this.bmpCore.SaveAsPng(this.outputStream, options);
    }

    [Benchmark(Description = "ImageSharp Palette NoDither Png")]
    public void PngCorePaletteNoDither()
    {
        this.outputStream.Position = 0;
        PngEncoder options = new() { Quantizer = new WebSafePaletteQuantizer(new QuantizerOptions { Dither = null }) };
        this.bmpCore.SaveAsPng(this.outputStream, options);
    }

    [Benchmark(Description = "ImageSharp Wu Png")]
    public void PngCoreWu()
    {
        this.outputStream.Position = 0;
        PngEncoder options = new() { Quantizer = KnownQuantizers.Wu };
        this.bmpCore.SaveAsPng(this.outputStream, options);
    }

    [Benchmark(Description = "ImageSharp Wu NoDither Png")]
    public void PngCoreWuNoDither()
    {
        this.outputStream.Position = 0;
        PngEncoder options = new() { Quantizer = new WuQuantizer(new QuantizerOptions { Dither = null }), ColorType = PngColorType.Palette };
        this.bmpCore.SaveAsPng(this.outputStream, options);
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                                 | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
    |--------------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
    | 'ImageSharp Hexadecatree Png'          |  8.073 ms | 2.5664 ms | 0.1407 ms |  1.00 |    0.02 |         - |         - |         - |   2.24 KB |        1.00 |
    | 'ImageSharp Hexadecatree NoDither Png' |  8.125 ms | 1.9564 ms | 0.1072 ms |  1.01 |    0.02 |         - |         - |         - |   2.31 KB |        1.03 |
    | 'ImageSharp Palette Png'               |  8.058 ms | 1.4806 ms | 0.0812 ms |  1.00 |    0.02 |         - |         - |         - |   2.24 KB |        1.00 |
    | 'ImageSharp Palette NoDither Png'      |  7.986 ms | 0.7053 ms | 0.0387 ms |  0.99 |    0.02 |         - |         - |         - |   2.37 KB |        1.06 |
    | 'ImageSharp Wu Png'                    |  8.039 ms | 1.1671 ms | 0.0640 ms |  1.00 |    0.02 |         - |         - |         - |   2.24 KB |        1.00 |
    | 'ImageSharp Wu NoDither Png'           | 29.626 ms | 8.9982 ms | 0.4932 ms |  3.67 |    0.08 | 1937.5000 | 1937.5000 | 1937.5000 | 584.75 KB |      260.68 |

    Allocated reports managed memory only.
    Input: Bmp/Car.bmp.
    */
}
