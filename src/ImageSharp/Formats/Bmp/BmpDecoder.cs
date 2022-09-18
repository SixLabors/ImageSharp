// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Image decoder for generating an image out of a Windows bitmap stream.
/// </summary>
public class BmpDecoder : IImageDecoderSpecialized<BmpDecoderOptions>
{
    /// <inheritdoc/>
    IImageInfo IImageInfoDetector.Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new BmpDecoderCore(new() { GeneralOptions = options }).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    Image<TPixel> IImageDecoder.Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => ((IImageDecoderSpecialized<BmpDecoderOptions>)this).Decode<TPixel>(new() { GeneralOptions = options }, stream, cancellationToken);

    /// <inheritdoc/>
    Image IImageDecoder.Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
      => ((IImageDecoderSpecialized<BmpDecoderOptions>)this).Decode(new() { GeneralOptions = options }, stream, cancellationToken);

    /// <inheritdoc/>
    Image<TPixel> IImageDecoderSpecialized<BmpDecoderOptions>.Decode<TPixel>(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        Image<TPixel>? image = new BmpDecoderCore(options).Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

        ArgumentNullException.ThrowIfNull(image);

        ImageDecoderUtilities.Resize(options.GeneralOptions, image);

        return image;
    }

    /// <inheritdoc/>
    Image IImageDecoderSpecialized<BmpDecoderOptions>.Decode(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => ((IImageDecoderSpecialized<BmpDecoderOptions>)this).Decode<Rgba32>(options, stream, cancellationToken);
}
