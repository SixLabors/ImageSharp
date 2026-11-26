// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Decodes one WebP file of each layout to <see cref="Rgba32"/> pixels.
/// </summary>
/// <remarks>
/// Magick decodes through libwebp, the reference implementation. Each layout reads a fixed file, so each build decodes
/// the same bytes.
/// </remarks>
[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class DecodeWebp
{
    private byte[] webpBytes;

    // Both libraries read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream webpStream;

    /// <summary>
    /// Gets or sets the WebP layout: lossy opaque (VP8, 3648 x 2736), lossy with alpha (ALPH and VP8, 2790 x 2776),
    /// or lossless (VP8L, 2790 x 2776).
    /// </summary>
    [Params("LossyOpaque", "LossyAlpha", "Lossless")]
    public string Layout { get; set; }

    /// <summary>
    /// Reads the file of <see cref="Layout"/>.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        string file = this.Layout switch
        {
            "LossyOpaque" => TestImages.Webp.Lossy.WithXmp,
            "LossyAlpha" => TestImages.Webp.Lossy.Earth,
            _ => TestImages.Webp.Lossless.Earth,
        };

        this.webpBytes = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, file));
        this.webpStream = new MemoryStream(this.webpBytes, false);
    }

    /// <summary>
    /// Releases the input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.webpStream.Dispose();
        this.webpStream = null;
    }

    [Benchmark(Baseline = true, Description = "Magick Webp")]
    public uint WebpMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.WebP };
        this.webpStream.Position = 0;
        using MagickImage image = new(this.webpStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Webp")]
    public int WebpImageSharp()
    {
        this.webpStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(this.webpStream);
        return image.Height;
    }
}
