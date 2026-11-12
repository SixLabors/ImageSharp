// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

public abstract class EncodeGif
{
    private FileStream bmpStream;
    private Image<Rgba32> bmpCore;
    private MagickImageCollection magickImage;

    // Both libraries write into this stream. Each call rewinds it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    private MemoryStream outputStream;

    protected abstract GifEncoder Encoder { get; }

    [Params(TestImages.Gif.Leo, TestImages.Gif.Cheers)]
    public string TestImage { get; set; }

    /// <summary>
    /// Loads full animation frames for both encoders before measurement.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);
            this.bmpStream = File.OpenRead(filePath);
            this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
            this.bmpStream.Position = 0;
            this.magickImage = new MagickImageCollection(this.bmpStream);

            // ImageSharp supplies full pixel frames to its encoder. Expand Magick's changed rectangles too,
            // then remove its source palette so each measured encoder must quantize pixels for GIF output.
            this.magickImage.Coalesce();

            foreach (IMagickImage<ushort> frame in this.magickImage)
            {
                frame.ColorType = ColorType.TrueColorAlpha;
            }

            this.outputStream = new MemoryStream();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpStream.Dispose();
        this.bmpStream = null;
        this.bmpCore.Dispose();
        this.magickImage.Dispose();
        this.outputStream.Dispose();
    }

    [Benchmark(Description = "ImageSharp Gif")]
    public void GifImageSharp()
    {
        this.outputStream.Position = 0;
        this.bmpCore.SaveAsGif(this.outputStream, this.Encoder);
    }

    [Benchmark(Baseline = true, Description = "Magick.NET Gif")]
    public void GifMagickNet()
    {
        this.outputStream.Position = 0;
        this.magickImage.Write(this.outputStream, MagickFormat.Gif);
    }
}

public class EncodeGif_DefaultEncoder : EncodeGif
{
    protected override GifEncoder Encoder => new();

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      DefaultJob : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4


    | Method           | TestImage      | Mean      | Error    | StdDev   | Ratio | RatioSD |
    |----------------- |--------------- |----------:|---------:|---------:|------:|--------:|
    | 'ImageSharp Gif' | Gif/cheers.gif | 155.87 ms | 1.424 ms | 1.332 ms |  1.00 |    0.01 |
    | 'Magick.NET Gif' | Gif/cheers.gif | 156.23 ms | 0.911 ms | 0.761 ms |  1.00 |    0.01 |
    |                  |                |           |          |          |       |         |
    | 'ImageSharp Gif' | Gif/leo.gif    |  17.35 ms | 0.318 ms | 0.402 ms |  1.43 |    0.04 |
    | 'Magick.NET Gif' | Gif/leo.gif    |  12.15 ms | 0.191 ms | 0.149 ms |  1.00 |    0.02 |

    Both encoders receive full animation frames. Magick source palettes are removed before measurement so its writer must quantize the pixels.
    The existing default job has no memory diagnoser, so this table does not report allocations.
    */
}

public class EncodeGif_CoarsePaletteEncoder : EncodeGif
{
    protected override GifEncoder Encoder => new()
    {
        Quantizer = new WebSafePaletteQuantizer(new QuantizerOptions { Dither = KnownDitherings.Bayer4x4, ColorMatchingMode = ColorMatchingMode.Coarse })
    };

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      DefaultJob : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4


    | Method           | TestImage      | Mean      | Error    | StdDev   | Ratio |
    |----------------- |--------------- |----------:|---------:|---------:|------:|
    | 'ImageSharp Gif' | Gif/cheers.gif | 264.28 ms | 1.677 ms | 1.487 ms |  1.74 |
    | 'Magick.NET Gif' | Gif/cheers.gif | 151.64 ms | 0.672 ms | 0.629 ms |  1.00 |
    |                  |                |           |          |          |       |
    | 'ImageSharp Gif' | Gif/leo.gif    |  25.34 ms | 0.077 ms | 0.072 ms |  2.15 |
    | 'Magick.NET Gif' | Gif/leo.gif    |  11.77 ms | 0.019 ms | 0.018 ms |  1.00 |

    Both encoders receive full animation frames. Magick source palettes are removed before measurement so its writer must quantize the pixels.
    The existing default job has no memory diagnoser, so this table does not report allocations.
    */
}
