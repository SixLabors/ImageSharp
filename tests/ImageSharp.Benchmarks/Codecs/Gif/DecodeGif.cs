// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class DecodeGif
{
    private byte[] gifBytes;

    private string TestImageFullPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [GlobalSetup]
    public void ReadImages() => this.gifBytes ??= File.ReadAllBytes(this.TestImageFullPath);

    [Params(TestImages.Gif.Cheers)]
    public string TestImage { get; set; }

    /// <summary>
    /// Decodes and renders every GIF animation frame with System.Drawing.
    /// </summary>
    /// <returns>The animation canvas size.</returns>
    [Benchmark(Baseline = true, Description = "System.Drawing Gif")]
    public SDSize GifSystemDrawing()
    {
        using MemoryStream memoryStream = new(this.gifBytes);
        using SDImage image = SDImage.FromStream(memoryStream);

        // Select and render every animation frame. Reading Size alone does not request the later frames.
        // Reuse one destination bitmap so rendering forces pixel decoding without allocating one bitmap per frame.
        using Bitmap destination = new(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(destination);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        int frameCount = image.GetFrameCount(FrameDimension.Time);

        for (int i = 0; i < frameCount; i++)
        {
            image.SelectActiveFrame(FrameDimension.Time, i);
            graphics.DrawImageUnscaled(image, 0, 0);
        }

        return image.Size;
    }

    /// <summary>
    /// Decodes and composes every GIF animation frame with ImageMagick.
    /// </summary>
    /// <returns>The number of decoded frames.</returns>
    [Benchmark(Description = "Magick Gif")]
    public int GifMagick()
    {
        using MemoryStream memoryStream = new(this.gifBytes);
        MagickReadSettings settings = new() { Format = MagickFormat.Gif };
        using MagickImageCollection images = new(memoryStream, settings);

        // GIF frames can store only changed rectangles. Compose them into full frames, as ImageSharp does.
        images.Coalesce();
        return images.Count;
    }

    [Benchmark(Description = "ImageSharp Gif")]
    public Size GifImageSharp()
    {
        using MemoryStream memoryStream = new(this.gifBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return new Size(image.Width, image.Height);
    }

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                | TestImage       |       Mean |      Error |    StdDev |   Ratio |   RatioSD |        Gen0 |        Gen1 |        Gen2 |    Allocated |   Alloc Ratio |
    | --------------------- | --------------- | ---------: | ---------: | --------: | ------: | --------: | ----------: | ----------: | ----------: | -----------: | ------------: |
    | 'System.Drawing Gif'  | Gif/cheers.gif  |   127.9 ms |   22.28 ms |   1.22 ms |    1.00 |      0.01 |           - |           - |           - |   3011.91 KB |          1.00 |
    | 'Magick Gif'          | Gif/cheers.gif  |   507.8 ms |   37.76 ms |   2.07 ms |    3.97 |      0.04 |           - |           - |           - |    353.77 KB |          0.12 |
    | 'ImageSharp Gif'      | Gif/cheers.gif  |   101.9 ms |   10.78 ms |   0.59 ms |    0.80 |      0.01 |   2400.0000 |   2400.0000 |   2400.0000 |    339.57 KB |          0.11 |

    Allocated reports managed memory only.
    All methods decode the 93 frames of cheers.gif. System.Drawing renders into one reusable bitmap; Magick and ImageSharp retain full frames.
    */
}
