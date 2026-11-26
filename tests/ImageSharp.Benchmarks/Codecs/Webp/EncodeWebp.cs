// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using ImageMagick;
using ImageMagick.Formats;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Encodes one source image to each WebP layout with method 4 and quality 75.
/// </summary>
/// <remarks>
/// Magick encodes through libwebp, the reference implementation, with the same settings. The opaque layouts encode the
/// 2048 x 1536 photo. The alpha layout encodes the 2790 x 2776 lossless Earth image, which has transparent pixels.
/// </remarks>
[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class EncodeWebp
{
    private MagickImage webpMagick;
    private Image<Rgba32> webp;

    // Both libraries write into this stream. Each call rewinds it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    private MemoryStream outputStream;

    /// <summary>
    /// Gets or sets the WebP layout that both libraries write: lossy opaque, lossy with alpha, or lossless.
    /// </summary>
    [Params("LossyOpaque", "LossyAlpha", "Lossless")]
    public string Layout { get; set; }

    /// <summary>
    /// Loads the source image of <see cref="Layout"/> for both libraries.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        string file = this.Layout == "LossyAlpha" ? TestImages.Webp.Lossless.Earth : TestImages.Jpeg.Baseline.Jpeg420Exif;
        string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, file);
        this.webp = Image.Load<Rgba32>(path);
        this.webpMagick = new MagickImage(path);
        this.outputStream = new MemoryStream();
    }

    /// <summary>
    /// Releases the images and the stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.webp.Dispose();
        this.webpMagick.Dispose();
        this.outputStream.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Magick Webp")]
    public void MagickWebp()
    {
        this.outputStream.Position = 0;

        // libwebp compresses alpha by default. The lossy settings match the ImageSharp settings below.
        WebPWriteDefines defines = this.Layout == "Lossless"
            ? new() { Lossless = true, Method = 4 }
            : new()
            {
                Lossless = false,
                Method = 4,
                AlphaCompression = WebPAlphaCompression.Compressed,
                FilterStrength = 60,
                SnsStrength = 50,
                Pass = 1
            };

        this.webpMagick.Quality = 75;
        this.webpMagick.Write(this.outputStream, defines);
    }

    [Benchmark(Description = "ImageSharp Webp")]
    public void ImageSharpWebp()
    {
        this.outputStream.Position = 0;
        WebpEncoder encoder = this.Layout == "Lossless"
            ? new()
            {
                FileFormat = WebpFileFormatType.Lossless,
                Method = WebpEncodingMethod.Level4,
                NearLossless = false,
                Quality = 75,

                // This is equal to exact = false in libwebp, which is the default.
                TransparentColorMode = TransparentColorMode.Clear
            }
            : new()
            {
                FileFormat = WebpFileFormatType.Lossy,
                Method = WebpEncodingMethod.Level4,
                Quality = 75,
                UseAlphaCompression = true,
                FilterStrength = 60,
                SpatialNoiseShaping = 50,
                EntropyPasses = 1
            };

        this.webp.Save(this.outputStream, encoder);
    }
}
