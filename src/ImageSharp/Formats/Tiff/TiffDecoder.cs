// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Image decoder for generating an image out of a TIFF stream.
    /// </summary>
    public class TiffDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        IImageInfo IImageInfoDetector.Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            return new TiffDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
        }

        /// <inheritdoc/>
        Image<TPixel> IImageDecoder.Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            TiffDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<TPixel>(options.Configuration, stream, cancellationToken);

            ImageDecoderUtilities.Resize(options, image);

            return image;
        }

        /// <inheritdoc/>
        Image IImageDecoder.Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => ((IImageDecoder)this).Decode<Rgba32>(options, stream, cancellationToken);
    }
}
