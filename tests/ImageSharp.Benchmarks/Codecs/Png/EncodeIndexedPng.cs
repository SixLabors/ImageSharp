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

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Bmp.Car));
            this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
            this.bmpStream.Position = 0;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpStream.Dispose();
        this.bmpStream = null;
        this.bmpCore.Dispose();
    }

    [Benchmark(Baseline = true, Description = "ImageSharp Octree Png")]
    public void PngCoreOctree()
    {
        using MemoryStream memoryStream = new();
        PngEncoder options = new() { Quantizer = KnownQuantizers.Octree };
        this.bmpCore.SaveAsPng(memoryStream, options);
    }

    [Benchmark(Description = "ImageSharp Octree NoDither Png")]
    public void PngCoreOctreeNoDither()
    {
        using MemoryStream memoryStream = new();
        PngEncoder options = new() { Quantizer = new OctreeQuantizer(new QuantizerOptions { Dither = null }) };
        this.bmpCore.SaveAsPng(memoryStream, options);
    }

    [Benchmark(Description = "ImageSharp Palette Png")]
    public void PngCorePalette()
    {
        using MemoryStream memoryStream = new();
        PngEncoder options = new() { Quantizer = KnownQuantizers.WebSafe };
        this.bmpCore.SaveAsPng(memoryStream, options);
    }

    [Benchmark(Description = "ImageSharp Palette NoDither Png")]
    public void PngCorePaletteNoDither()
    {
        using MemoryStream memoryStream = new();
        PngEncoder options = new() { Quantizer = new WebSafePaletteQuantizer(new QuantizerOptions { Dither = null }) };
        this.bmpCore.SaveAsPng(memoryStream, options);
    }

    [Benchmark(Description = "ImageSharp Wu Png")]
    public void PngCoreWu()
    {
        using MemoryStream memoryStream = new();
        PngEncoder options = new() { Quantizer = KnownQuantizers.Wu };
        this.bmpCore.SaveAsPng(memoryStream, options);
    }

    [Benchmark(Description = "ImageSharp Wu NoDither Png")]
    public void PngCoreWuNoDither()
    {
        using MemoryStream memoryStream = new();
        PngEncoder options = new() { Quantizer = new WuQuantizer(new QuantizerOptions { Dither = null }), ColorType = PngColorType.Palette };
        this.bmpCore.SaveAsPng(memoryStream, options);
    }
}
