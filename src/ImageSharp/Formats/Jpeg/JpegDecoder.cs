// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Decoder for generating an image out of a jpeg encoded stream.
    /// </summary>
    public sealed class JpegDecoder : IImageDecoderSpecialized<JpegDecoderOptions>
    {
        /// <inheritdoc/>
        IImageInfo IImageInfoDetector.Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            using JpegDecoderCore decoder = new(new() { GeneralOptions = options });
            return decoder.Identify(options.Configuration, stream, cancellationToken);
        }

        /// <inheritdoc/>
        Image<TPixel> IImageDecoder.Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
             => ((IImageDecoderSpecialized<JpegDecoderOptions>)this).Decode<TPixel>(new() { GeneralOptions = options }, stream, cancellationToken);

        /// <inheritdoc/>
        Image IImageDecoder.Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => ((IImageDecoderSpecialized<JpegDecoderOptions>)this).Decode(new() { GeneralOptions = options }, stream, cancellationToken);

        /// <inheritdoc/>
        Image<TPixel> IImageDecoderSpecialized<JpegDecoderOptions>.Decode<TPixel>(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            using JpegDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            if (options.ResizeMode != JpegDecoderResizeMode.IdctOnly)
            {
                ImageDecoderUtilities.Resize(options.GeneralOptions, image);
            }

            return image;
        }

        /// <inheritdoc/>
        Image IImageDecoderSpecialized<JpegDecoderOptions>.Decode(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => ((IImageDecoderSpecialized<JpegDecoderOptions>)this).Decode<Rgb24>(options, stream, cancellationToken);
    }
}
