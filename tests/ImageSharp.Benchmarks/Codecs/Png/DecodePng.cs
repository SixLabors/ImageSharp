// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Decodes one photo, stored in each PNG color type and bit depth.
/// </summary>
/// <remarks>
/// The setup encodes the 2048 x 1536 photo with no filter, so each run measures the inflate and the row conversion of
/// the layout. DecodeFilteredPng and PngFilterDecode measure the filters. Both libraries decode the same bytes.
/// </remarks>
[Config(typeof(Config.Short))]
public class DecodePng
{
    private byte[] pngBytes;

    // Both libraries read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream inputStream;

    /// <summary>
    /// Gets or sets the PNG layout: the color type, the bit depth, and Adam7 for the interlaced layout.
    /// </summary>
    [Params(
        "Gray1",
        "Gray4",
        "Gray8",
        "Gray16",
        "GrayAlpha8",
        "GrayAlpha16",
        "Rgb8",
        "Rgb16",
        "Rgba8",
        "Rgba16",
        "Palette1",
        "Palette4",
        "Palette8",
        "Rgba8Adam7")]
    public string Layout { get; set; }

    /// <summary>
    /// Encodes the photo in the layout of <see cref="Layout"/>.
    /// </summary>
    [GlobalSetup]
    public void EncodeImage()
    {
        string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.Baseline.Jpeg420Exif);
        using Image<Rgba32> source = Image.Load<Rgba32>(path);

        PngEncoder encoder = this.Layout switch
        {
            "Gray1" => CreateEncoder(PngColorType.Grayscale, PngBitDepth.Bit1),
            "Gray4" => CreateEncoder(PngColorType.Grayscale, PngBitDepth.Bit4),
            "Gray8" => CreateEncoder(PngColorType.Grayscale, PngBitDepth.Bit8),
            "Gray16" => CreateEncoder(PngColorType.Grayscale, PngBitDepth.Bit16),
            "GrayAlpha8" => CreateEncoder(PngColorType.GrayscaleWithAlpha, PngBitDepth.Bit8),
            "GrayAlpha16" => CreateEncoder(PngColorType.GrayscaleWithAlpha, PngBitDepth.Bit16),
            "Rgb8" => CreateEncoder(PngColorType.Rgb, PngBitDepth.Bit8),
            "Rgb16" => CreateEncoder(PngColorType.Rgb, PngBitDepth.Bit16),
            "Rgba8" => CreateEncoder(PngColorType.RgbWithAlpha, PngBitDepth.Bit8),
            "Rgba16" => CreateEncoder(PngColorType.RgbWithAlpha, PngBitDepth.Bit16),
            "Palette1" => CreateEncoder(PngColorType.Palette, PngBitDepth.Bit1),
            "Palette4" => CreateEncoder(PngColorType.Palette, PngBitDepth.Bit4),
            "Palette8" => CreateEncoder(PngColorType.Palette, PngBitDepth.Bit8),
            _ => CreateEncoder(PngColorType.RgbWithAlpha, PngBitDepth.Bit8, PngInterlaceMode.Adam7),
        };

        using MemoryStream encoded = new();
        source.SaveAsPng(encoded, encoder);
        this.pngBytes = encoded.ToArray();
        this.inputStream = new MemoryStream(this.pngBytes, false);
    }

    /// <summary>
    /// Releases the shared input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Png")]
    public SDSize PngSystemDrawing()
    {
        this.inputStream.Position = 0;
        using SDImage image = SDImage.FromStream(this.inputStream);
        return image.Size;
    }

    [Benchmark(Description = "ImageSharp Png")]
    public Size PngImageSharp()
    {
        this.inputStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.inputStream);
        return image.Size;
    }

    /// <summary>
    /// Creates the encoder of one layout with no filter.
    /// </summary>
    /// <param name="colorType">The color type.</param>
    /// <param name="bitDepth">The bit depth.</param>
    /// <param name="interlace">The interlace mode.</param>
    /// <returns>The encoder.</returns>
    private static PngEncoder CreateEncoder(PngColorType colorType, PngBitDepth bitDepth, PngInterlaceMode interlace = PngInterlaceMode.None)
        => new()
        {
            ColorType = colorType,
            BitDepth = bitDepth,
            InterlaceMethod = interlace,
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

    | Method               | Layout      | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
    |--------------------- |------------ |------------:|------------:|----------:|------:|--------:|----------:|------------:|
    | 'System.Drawing Png' | Gray1       |    834.4 us |    262.5 us |  14.39 us |  1.00 |    0.02 |     328 B |        1.00 |
    | 'ImageSharp Png'     | Gray1       |  1,554.6 us |    291.5 us |  15.98 us |  1.86 |    0.03 |    7620 B |       23.23 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Gray16      | 31,895.0 us |  1,485.1 us |  81.40 us |  1.00 |    0.00 |     355 B |        1.00 |
    | 'ImageSharp Png'     | Gray16      | 14,554.4 us |  2,035.3 us | 111.56 us |  0.46 |    0.00 |    7645 B |       21.54 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Gray4       |  5,628.1 us |    221.8 us |  12.16 us |  1.00 |    0.00 |     331 B |        1.00 |
    | 'ImageSharp Png'     | Gray4       |  3,695.6 us |    208.7 us |  11.44 us |  0.66 |    0.00 |    7623 B |       23.03 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Gray8       |  9,682.5 us |    317.2 us |  17.38 us |  1.00 |    0.00 |     335 B |        1.00 |
    | 'ImageSharp Png'     | Gray8       |  7,355.1 us |  2,255.0 us | 123.60 us |  0.76 |    0.01 |    7486 B |       22.35 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | GrayAlpha16 | 33,748.5 us |  2,125.1 us | 116.48 us |  1.00 |    0.00 |     365 B |        1.00 |
    | 'ImageSharp Png'     | GrayAlpha16 | 27,267.2 us |  2,250.3 us | 123.34 us |  0.81 |    0.00 |    7673 B |       21.02 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | GrayAlpha8  | 13,650.0 us |  2,751.3 us | 150.81 us |  1.00 |    0.01 |     335 B |        1.00 |
    | 'ImageSharp Png'     | GrayAlpha8  | 11,584.8 us |  4,056.7 us | 222.36 us |  0.85 |    0.02 |    7501 B |       22.39 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Palette1    |    926.0 us |    292.8 us |  16.05 us |  1.00 |    0.02 |     328 B |        1.00 |
    | 'ImageSharp Png'     | Palette1    |  1,803.6 us |    524.6 us |  28.75 us |  1.95 |    0.04 |    7927 B |       24.17 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Palette4    |  3,546.9 us |    101.7 us |   5.57 us |  1.00 |    0.00 |     330 B |        1.00 |
    | 'ImageSharp Png'     | Palette4    |  4,250.1 us |  1,303.8 us |  71.46 us |  1.20 |    0.02 |    8422 B |       25.52 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Palette8    |  7,728.9 us |  4,726.6 us | 259.08 us |  1.00 |    0.04 |     335 B |        1.00 |
    | 'ImageSharp Png'     | Palette8    |  8,244.0 us |  9,442.9 us | 517.60 us |  1.07 |    0.07 |   16837 B |       50.26 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Rgb16       | 45,432.8 us |  4,435.6 us | 243.13 us |  1.00 |    0.01 |     385 B |        1.00 |
    | 'ImageSharp Png'     | Rgb16       | 37,169.2 us |  2,912.4 us | 159.64 us |  0.82 |    0.00 |    7757 B |       20.15 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Rgb8        | 22,800.3 us |  3,528.0 us | 193.38 us |  1.00 |    0.01 |     342 B |        1.00 |
    | 'ImageSharp Png'     | Rgb8        | 20,334.2 us |  4,279.8 us | 234.59 us |  0.89 |    0.01 |    7529 B |       22.01 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Rgba16      | 51,256.2 us | 11,067.7 us | 606.66 us |  1.00 |    0.01 |     395 B |        1.00 |
    | 'ImageSharp Png'     | Rgba16      | 41,535.6 us |  2,941.7 us | 161.24 us |  0.81 |    0.01 |    7757 B |       19.64 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Rgba8       | 28,987.1 us |    802.2 us |  43.97 us |  1.00 |    0.00 |     342 B |        1.00 |
    | 'ImageSharp Png'     | Rgba8       | 25,601.1 us |  1,158.2 us |  63.49 us |  0.88 |    0.00 |    7529 B |       22.01 |
    |                      |             |             |             |           |       |         |           |             |
    | 'System.Drawing Png' | Rgba8Adam7  | 30,598.1 us |  1,475.2 us |  80.86 us |  1.00 |    0.00 |     342 B |        1.00 |
    | 'ImageSharp Png'     | Rgba8Adam7  | 27,350.2 us |  1,377.0 us |  75.48 us |  0.89 |    0.00 |    7673 B |       22.44 |

    Allocated reports managed memory only.
    */
}
