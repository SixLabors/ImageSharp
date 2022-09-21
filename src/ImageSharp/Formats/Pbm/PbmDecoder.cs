// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Image decoder for reading PGM, PBM or PPM bitmaps from a stream. These images are from
/// the family of PNM images.
/// <list type="bullet">
/// <item>
/// <term>PBM</term>
/// <description>Black and white images.</description>
/// </item>
/// <item>
/// <term>PGM</term>
/// <description>Grayscale images.</description>
/// </item>
/// <item>
/// <term>PPM</term>
/// <description>Color images, with RGB pixels.</description>
/// </item>
/// </list>
/// The specification of these images is found at <seealso href="http://netpbm.sourceforge.net/doc/pnm.html"/>.
/// </summary>
public sealed class PbmDecoder : IImageDecoder
{
    /// <inheritdoc/>
    IImageInfo IImageInfoDetector.Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new PbmDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc />
    Image<TPixel> IImageDecoder.Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        PbmDecoderCore decoder = new(options);
        Image<TPixel> image = decoder.Decode<TPixel>(options.Configuration, stream, cancellationToken);

        ArgumentNullException.ThrowIfNull(image);

        ImageDecoderUtilities.Resize(options, image);

        return image;
    }

    /// <inheritdoc />
    Image IImageDecoder.Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => ((IImageDecoder)this).Decode<Rgb24>(options, stream, cancellationToken);
}
