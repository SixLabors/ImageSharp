// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using ImageMagick;
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
    private MagickImageCollection magickImage;

    protected abstract GifEncoder Encoder { get; }

    [Params(TestImages.Gif.Leo, TestImages.Gif.Cheers)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);
            this.bmpStream = File.OpenRead(filePath);
            this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
            this.bmpStream.Position = 0;
            this.bmpDrawing = SDImage.FromStream(this.bmpStream);

            this.bmpStream.Position = 0;
            this.magickImage = new MagickImageCollection(this.bmpStream);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpStream.Dispose();
        this.bmpStream = null;
        this.bmpCore.Dispose();
        this.bmpDrawing.Dispose();
        this.magickImage.Dispose();
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

    [Benchmark(Description = "Magick.NET Gif")]
    public void GifMagickNet()
    {
        using MemoryStream ms = new();
        this.magickImage.Write(ms, MagickFormat.Gif);
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
