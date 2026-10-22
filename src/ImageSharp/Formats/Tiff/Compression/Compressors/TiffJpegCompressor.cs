// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

internal class TiffJpegCompressor : TiffBaseCompressor
{
    /// <summary>
    /// The configuration used to create the embedded JPEG image.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffJpegCompressor"/> class.
    /// </summary>
    /// <param name="output">The TIFF output stream.</param>
    /// <param name="configuration">The configuration used by the TIFF encoder.</param>
    /// <param name="width">The strip width in pixels.</param>
    /// <param name="bitsPerPixel">The stored bits per pixel.</param>
    /// <param name="predictor">The horizontal predictor.</param>
    public TiffJpegCompressor(Stream output, Configuration configuration, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
        : base(output, configuration.MemoryAllocator, width, bitsPerPixel, predictor)
    {
        this.configuration = configuration;
    }

    /// <inheritdoc/>
    public override TiffCompression Method => TiffCompression.Jpeg;

    /// <inheritdoc/>
    public override void Initialize(int rowsPerStrip)
    {
    }

    /// <inheritdoc/>
    public override void CompressStrip(Span<byte> rows, int height)
    {
        int pixelCount = rows.Length / 3;
        int width = pixelCount / height;

        using MemoryStream memoryStream = new();

        // The embedded image uses the caller's allocator and must not initialize the default codec registry.
        using Image<Rgb24> image = Image.LoadPixelData<Rgb24>(this.configuration, rows, width, height);

        image.Save(memoryStream, new JpegEncoder()
        {
            ColorType = JpegColorType.Rgb
        });
        memoryStream.Position = 0;
        memoryStream.WriteTo(this.Output);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
