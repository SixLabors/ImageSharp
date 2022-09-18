// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.OpenExr;

/// <summary>
/// Image decoder for generating an image out of a OpenExr stream.
/// </summary>
public class ExrDecoder : IImageDecoderSpecialized<ExrDecoderOptions>
{
    /// <inheritdoc/>
    IImageInfo IImageInfoDetector.Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new ExrDecoderCore(new() { GeneralOptions = options }).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    Image<TPixel> IImageDecoder.Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => ((IImageDecoderSpecialized<ExrDecoderOptions>)this).Decode<TPixel>(new() { GeneralOptions = options }, stream, cancellationToken);

    /// <inheritdoc/>
    Image IImageDecoder.Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => ((IImageDecoderSpecialized<ExrDecoderOptions>)this).Decode(new() { GeneralOptions = options }, stream, cancellationToken);

    /// <inheritdoc/>
    Image<TPixel> IImageDecoderSpecialized<ExrDecoderOptions>.Decode<TPixel>(ExrDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        Image<TPixel> image = new ExrDecoderCore(options).Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

        ImageDecoderUtilities.Resize(options.GeneralOptions, image);

        return image;
    }

    /// <inheritdoc/>
    Image IImageDecoderSpecialized<ExrDecoderOptions>.Decode(ExrDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => ((IImageDecoderSpecialized<ExrDecoderOptions>)this).Decode<Rgba32>(options, stream, cancellationToken);
}
