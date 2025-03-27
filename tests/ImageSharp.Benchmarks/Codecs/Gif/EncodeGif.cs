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

public abstract class EncodeGif
{
    // System.Drawing needs this.
    private FileStream bmpStream;
    private SDImage bmpDrawing;
    private Image<Rgba32> bmpCore;

    protected abstract GifEncoder Encoder { get; }

    [Params(TestImages.Gif.Leo, TestImages.Gif.Cheers)]
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
        this.bmpCore.SaveAsGif(memoryStream, this.Encoder);
    }
}

public class EncodeGif_DefaultEncoder : EncodeGif
{
    protected override GifEncoder Encoder => new();
}

public class EncodeGif_CoarsePaletteEncoder : EncodeGif
{
    protected override GifEncoder Encoder => new()
    {
        Quantizer = new WebSafePaletteQuantizer(new QuantizerOptions { Dither = KnownDitherings.Bayer4x4, ColorMatchingMode = ColorMatchingMode.Coarse })
    };
}
