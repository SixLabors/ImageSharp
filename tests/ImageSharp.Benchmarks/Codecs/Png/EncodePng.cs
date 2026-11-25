// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Encodes one photo to each 8-bit and 16-bit PNG layout.
/// </summary>
/// <remarks>
/// ImageSharp encodes from pixels of the layout pixel type with no filter, so each run measures the row collection and
/// the compression, with no pixel type conversion before the encoder. System.Drawing cannot select a filter or a
/// layout, so it encodes the photo in its own layout for each case. PngFilterEncode measures the filters.
/// </remarks>
[Config(typeof(Config.Short))]
public class EncodePng
{
    // System.Drawing needs this.
    private FileStream sourceStream;
    private SDImage sourceDrawing;
    private Image sourceCore;
    private PngEncoder encoder;

    // Both libraries write into this stream. Each call rewinds it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    private MemoryStream outputStream;

    /// <summary>
    /// Gets or sets the PNG layout that ImageSharp writes: the color type and the bit depth.
    /// </summary>
    [Params("Gray16", "GrayAlpha16", "Rgb8", "Rgb16", "Rgba8", "Rgba16")]
    public string Layout { get; set; }

    /// <summary>
    /// Loads the photo for both libraries. ImageSharp loads it in the pixel type of <see cref="Layout"/>.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.Baseline.Jpeg420Exif);
        this.sourceCore = this.Layout switch
        {
            "Gray16" => Image.Load<L16>(path),
            "GrayAlpha16" => Image.Load<La32>(path),
            "Rgb8" => Image.Load<Rgb24>(path),
            "Rgb16" => Image.Load<Rgb48>(path),
            "Rgba8" => Image.Load<Rgba32>(path),
            _ => Image.Load<Rgba64>(path),
        };

        this.encoder = this.Layout switch
        {
            "Gray16" => CreateEncoder(PngColorType.Grayscale, PngBitDepth.Bit16),
            "GrayAlpha16" => CreateEncoder(PngColorType.GrayscaleWithAlpha, PngBitDepth.Bit16),
            "Rgb8" => CreateEncoder(PngColorType.Rgb, PngBitDepth.Bit8),
            "Rgb16" => CreateEncoder(PngColorType.Rgb, PngBitDepth.Bit16),
            "Rgba8" => CreateEncoder(PngColorType.RgbWithAlpha, PngBitDepth.Bit8),
            _ => CreateEncoder(PngColorType.RgbWithAlpha, PngBitDepth.Bit16),
        };

        this.sourceStream = File.OpenRead(path);
        this.sourceDrawing = SDImage.FromStream(this.sourceStream);
        this.outputStream = new MemoryStream();
    }

    /// <summary>
    /// Releases the images and the streams.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.sourceStream.Dispose();
        this.sourceStream = null;
        this.sourceCore.Dispose();
        this.sourceDrawing.Dispose();
        this.outputStream.Dispose();
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Png")]
    public void PngSystemDrawing()
    {
        this.outputStream.Position = 0;
        this.sourceDrawing.Save(this.outputStream, ImageFormat.Png);
    }

    [Benchmark(Description = "ImageSharp Png")]
    public void PngCore()
    {
        this.outputStream.Position = 0;
        this.sourceCore.SaveAsPng(this.outputStream, this.encoder);
    }

    /// <summary>
    /// Creates the encoder of one layout with no filter.
    /// </summary>
    /// <param name="colorType">The color type.</param>
    /// <param name="bitDepth">The bit depth.</param>
    /// <returns>The encoder.</returns>
    private static PngEncoder CreateEncoder(PngColorType colorType, PngBitDepth bitDepth)
        => new()
        {
            ColorType = colorType,
            BitDepth = bitDepth,
            FilterMethod = PngFilterMethod.None,
        };

    /* Results 25.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.10075)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100
      [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method               | Layout      | Mean     | Error    | StdDev  | Ratio | RatioSD | Allocated | Alloc Ratio |
    |--------------------- |------------ |---------:|---------:|--------:|------:|--------:|----------:|------------:|
    | 'System.Drawing Png' | Gray16      | 118.1 ms |  2.31 ms | 0.13 ms |  1.00 |    0.00 |   3.25 KB |        1.00 |
    | 'ImageSharp Png'     | Gray16      | 102.3 ms | 11.15 ms | 0.61 ms |  0.87 |    0.00 |   9.39 KB |        2.89 |
    |                      |             |          |          |         |       |         |           |             |
    | 'System.Drawing Png' | GrayAlpha16 | 118.6 ms | 17.17 ms | 0.94 ms |  1.00 |    0.01 |   3.25 KB |        1.00 |
    | 'ImageSharp Png'     | GrayAlpha16 | 158.3 ms |  2.92 ms | 0.16 ms |  1.34 |    0.01 |   9.39 KB |        2.89 |
    |                      |             |          |          |         |       |         |           |             |
    | 'System.Drawing Png' | Rgb16       | 118.1 ms |  3.19 ms | 0.17 ms |  1.00 |    0.00 |   3.25 KB |        1.00 |
    | 'ImageSharp Png'     | Rgb16       | 242.7 ms |  5.84 ms | 0.32 ms |  2.06 |    0.00 |   9.55 KB |        2.93 |
    |                      |             |          |          |         |       |         |           |             |
    | 'System.Drawing Png' | Rgb8        | 123.6 ms | 44.18 ms | 2.42 ms |  1.00 |    0.02 |   3.25 KB |        1.00 |
    | 'ImageSharp Png'     | Rgb8        | 141.8 ms | 35.75 ms | 1.96 ms |  1.15 |    0.02 |   9.39 KB |        2.89 |
    |                      |             |          |          |         |       |         |           |             |
    | 'System.Drawing Png' | Rgba16      | 120.0 ms |  9.05 ms | 0.50 ms |  1.00 |    0.01 |   3.25 KB |        1.00 |
    | 'ImageSharp Png'     | Rgba16      | 355.3 ms | 26.66 ms | 1.46 ms |  2.96 |    0.01 |   9.66 KB |        2.97 |
    |                      |             |          |          |         |       |         |           |             |
    | 'System.Drawing Png' | Rgba8       | 119.9 ms | 16.94 ms | 0.93 ms |  1.00 |    0.01 |   3.25 KB |        1.00 |
    | 'ImageSharp Png'     | Rgba8       | 183.2 ms | 23.52 ms | 1.29 ms |  1.53 |    0.01 |   9.55 KB |        2.93 |

    Allocated reports managed memory only.
    */
}
