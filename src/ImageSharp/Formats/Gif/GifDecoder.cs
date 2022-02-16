// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Decoder for generating an image out of a gif encoded stream.
    /// </summary>
    public sealed class GifDecoder : IImageDecoder, IGifDecoderOptions, IImageInfoDetector
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; } = false;

        /// <summary>
        /// Gets or sets the decoding mode for multi-frame images
        /// </summary>
        public FrameDecodingMode DecodingMode { get; set; } = FrameDecodingMode.All;

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new GifDecoderCore(configuration, this);
            return decoder.Decode<TPixel>(configuration, stream, cancellationToken);
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream, CancellationToken cancellationToken = default)
            => this.Decode<Rgba32>(configuration, stream, cancellationToken);

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream, CancellationToken cancellationToken = default)
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new GifDecoderCore(configuration, this);

            using var bufferedStream = new BufferedReadStream(configuration, stream);
            return decoder.Identify(bufferedStream, cancellationToken);
        }
    }
}
