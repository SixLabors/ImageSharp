// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Decodes one photo, stored with each PNG filter, to <see cref="Rgba32"/> pixels.
/// </summary>
/// <remarks>
/// The setup encodes the 2048 x 1536 photo with one filter for all rows. Each row of the results decodes the same
/// pixels, so only the filter and the byte count of one pixel change between rows.
/// </remarks>
[Config(typeof(Config.Short))]
public class DecodeFilteredPng
{
    private byte[] pngBytes;

    // The decoder reads from this stream. Each call rewinds it, so the results measure the decoder only.
    private MemoryStream inputStream;

    /// <summary>
    /// Gets or sets the filter of all rows.
    /// </summary>
    [Params(PngFilterMethod.None, PngFilterMethod.Sub, PngFilterMethod.Up, PngFilterMethod.Average, PngFilterMethod.Paeth)]
    public PngFilterMethod Filter { get; set; }

    /// <summary>
    /// Gets or sets the PNG layout: 8-bit RGB has 3 bytes for each pixel, and 8-bit RGBA has 4.
    /// </summary>
    [Params("Rgb8", "Rgba8")]
    public string Layout { get; set; }

    /// <summary>
    /// Encodes the photo with <see cref="Filter"/> in the layout of <see cref="Layout"/>.
    /// </summary>
    [GlobalSetup]
    public void EncodeImage()
    {
        string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.Baseline.Jpeg420Exif);
        using Image<Rgba32> source = Image.Load<Rgba32>(path);

        PngEncoder encoder = new()
        {
            ColorType = this.Layout == "Rgb8" ? PngColorType.Rgb : PngColorType.RgbWithAlpha,
            BitDepth = PngBitDepth.Bit8,
            FilterMethod = this.Filter,
        };

        using MemoryStream encoded = new();
        source.SaveAsPng(encoded, encoder);
        this.pngBytes = encoded.ToArray();
        this.inputStream = new MemoryStream(this.pngBytes, false);
    }

    /// <summary>
    /// Releases the input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
    }

    /// <summary>
    /// Decodes the image.
    /// </summary>
    /// <returns>The image size.</returns>
    [Benchmark(Description = "ImageSharp Png")]
    public Size PngImageSharp()
    {
        this.inputStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.inputStream);
        return image.Size;
    }
}
