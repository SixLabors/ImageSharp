// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Image decoder for generating an image out of a TIFF stream.
/// </summary>
public class TiffDecoder : ImageDecoder
{
    private TiffDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static TiffDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new TiffDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        TiffDecoderCore decoder = new(options);
        Image<TPixel> image = decoder.Decode<TPixel>(options.Configuration, stream, cancellationToken);

        ScaleToTargetSize(options, image);

        return image;
    }

    /// <inheritdoc/>
    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        long position = stream.Position;
        ImageInfo info = this.Identify(options, stream, cancellationToken);
        stream.Position = position;

        TiffMetadata metadata = info.Metadata.GetTiffMetadata();
        if (metadata.SampleFormat == TiffSampleFormat.Float)
        {
            // TIFF samples are decoded once into the pixel type chosen from the root IFD.
            return metadata.ExtraSampleType == TiffExtraSampleType.AssociatedAlphaData
                ? this.Decode<RgbaVectorP>(options, stream, cancellationToken)
                : this.Decode<RgbaVector>(options, stream, cancellationToken);
        }

        if (metadata.SampleFormat == TiffSampleFormat.UnsignedInteger &&
            metadata.PhotometricInterpretation == TiffPhotometricInterpretation.Rgb)
        {
            // A default load must retain all 16 bits of each color sample.
            return metadata.BitsPerPixel switch
            {
                TiffBitsPerPixel.Bit48 => this.Decode<Rgb48>(options, stream, cancellationToken),
                TiffBitsPerPixel.Bit64 => this.Decode<Rgba64>(options, stream, cancellationToken),
                _ => this.Decode<Rgba32>(options, stream, cancellationToken),
            };
        }

        return this.Decode<Rgba32>(options, stream, cancellationToken);
    }
}
