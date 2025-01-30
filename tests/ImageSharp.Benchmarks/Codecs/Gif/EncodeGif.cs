// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class EncodeGif
{
    // System.Drawing needs this.
    private Stream bmpStream;
    private SDImage bmpDrawing;
    private Image<Rgba32> bmpCore;

    // Try to get as close to System.Drawing's output as possible
    private readonly GifEncoder encoder = new()
    {
        Quantizer = new WebSafePaletteQuantizer(new() { Dither = KnownDitherings.Bayer4x4 })
    };

    [Params(TestImages.Bmp.Car, TestImages.Png.Rgb48Bpp)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage));
            this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
            this.bmpStream.Position = 0;
            this.bmpDrawing = SDImage.FromStream(this.bmpStream);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpStream.Dispose();
        this.bmpStream = null;
        this.bmpCore.Dispose();
        this.bmpDrawing.Dispose();
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Gif")]
    public void GifSystemDrawing()
    {
        using MemoryStream memoryStream = new();
        this.bmpDrawing.Save(memoryStream, ImageFormat.Gif);
    }

    [Benchmark(Description = "ImageSharp Gif")]
    public void GifImageSharp()
    {
        using MemoryStream memoryStream = new();
        this.bmpCore.SaveAsGif(memoryStream, this.encoder);
    }
}
