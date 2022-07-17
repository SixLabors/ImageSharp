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
    public sealed class JpegDecoder : ImageDecoder<JpegDecoderOptions>
    {
        /// <inheritdoc/>
        /// <remarks>
        /// Unlike <see cref="IImageDecoder.Decode{TPixel}(DecoderOptions, Stream, CancellationToken)"/>, when
        /// <see cref="DecoderOptions.TargetSize"/> is passed, the codec may not be able to scale efficiently to
        /// the exact scale factor requested, so returns a size that approximates that scale.
        /// Upscaling is not supported, so the original size will be returned.
        /// </remarks>
        public override Image<TPixel> DecodeSpecialized<TPixel>(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            using JpegDecoderCore decoder = new(options);
            return decoder.Decode<JpegDecoderOptions, TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Unlike <see cref="IImageDecoder.Decode(DecoderOptions, Stream, CancellationToken)"/>, when
        /// <see cref="DecoderOptions.TargetSize"/> is passed, the codec may not be able to scale efficiently to
        /// the exact scale factor requested, so returns a size that approximates that scale.
        /// Upscaling is not supported, so the original size will be returned.
        /// </remarks>
        public override Image DecodeSpecialized(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<Rgb24>(options, stream, cancellationToken);

        /// <inheritdoc/>
        public override IImageInfo IdentifySpecialized(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            using JpegDecoderCore decoder = new(options);
            return decoder.Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        }
    }
}
