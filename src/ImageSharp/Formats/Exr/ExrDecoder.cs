// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Image decoder for generating an image out of a OpenExr stream.
/// </summary>
public class ExrDecoder : ImageDecoder
{
    private ExrDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static ExrDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new ExrDecoderCore(new ExrDecoderOptions { GeneralOptions = options }).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        ExrDecoderCore decoder = new(new ExrDecoderOptions { GeneralOptions = options });
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

        ExrMetadata metadata = info.Metadata.GetExrMetadata();

        // Match PNG's format-selected decode path. Only the header is read twice;
        // the selected pixel buffer is filled once by the generic decoder.
        return metadata.PixelType switch
        {
            ExrPixelType.Half when metadata.ImageDataType == ExrImageDataType.Rgba => this.Decode<RgbaHalfP>(options, stream, cancellationToken),
            ExrPixelType.Half => this.Decode<RgbaHalf>(options, stream, cancellationToken),
            ExrPixelType.Float when metadata.ImageDataType == ExrImageDataType.Rgba => this.Decode<RgbaVectorP>(options, stream, cancellationToken),
            ExrPixelType.Float => this.Decode<RgbaVector>(options, stream, cancellationToken),
            _ => this.Decode<Rgba32>(options, stream, cancellationToken)
        };
    }
}
