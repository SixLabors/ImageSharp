// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

public abstract class DecodeEncodeGif
{
    private MemoryStream outputStream;

    protected abstract GifEncoder Encoder { get; }

    [Params(TestImages.Gif.Leo, TestImages.Gif.Cheers)]
    public string TestImage { get; set; }

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [GlobalSetup]
    public void Setup() => this.outputStream = new MemoryStream();

    [GlobalCleanup]
    public void Cleanup() => this.outputStream.Close();

    [Benchmark(Baseline = true)]
    public void SystemDrawing()
    {
        this.outputStream.Position = 0;
        using SDImage image = SDImage.FromFile(this.TestImageFullPath);
        image.Save(this.outputStream, ImageFormat.Gif);
    }

    [Benchmark]
    public void ImageSharp()
    {
        this.outputStream.Position = 0;
        using Image image = Image.Load(this.TestImageFullPath);
        image.SaveAsGif(this.outputStream, this.Encoder);
    }
}

public class DecodeEncodeGif_DefaultEncoder : DecodeEncodeGif
{
    protected override GifEncoder Encoder => new();
}

public class DecodeEncodeGif_CoarsePaletteEncoder : DecodeEncodeGif
{
    protected override GifEncoder Encoder => new()
    {
        Quantizer = new WebSafePaletteQuantizer(new QuantizerOptions { Dither = KnownDitherings.Bayer4x4, ColorMatchingMode = ColorMatchingMode.Coarse })
    };
}
