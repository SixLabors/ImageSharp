// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
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
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new GifDecoderCore(configuration, this);

            try
            {
                using var bufferedStream = new BufferedReadStream(stream);
                return decoder.Decode<TPixel>(bufferedStream);
            }
            catch (InvalidMemoryOperationException ex)
            {
                Size dims = decoder.Dimensions;

                GifThrowHelper.ThrowInvalidImageContentException($"Cannot decode image. Failed to allocate buffers for possibly degenerate dimensions: {dims.Width}x{dims.Height}.", ex);

                // Not reachable, as the previous statement will throw a exception.
                return null;
            }
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream)
            => this.Decode<Rgba32>(configuration, stream);

        /// <inheritdoc/>
        public async Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new GifDecoderCore(configuration, this);

            try
            {
                using var bufferedStream = new BufferedReadStream(stream);
                return await decoder.DecodeAsync<TPixel>(bufferedStream).ConfigureAwait(false);
            }
            catch (InvalidMemoryOperationException ex)
            {
                Size dims = decoder.Dimensions;

                GifThrowHelper.ThrowInvalidImageContentException($"Cannot decode image. Failed to allocate buffers for possibly degenerate dimensions: {dims.Width}x{dims.Height}.", ex);

                // Not reachable, as the previous statement will throw a exception.
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream)
            => await this.DecodeAsync<Rgba32>(configuration, stream).ConfigureAwait(false);

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new GifDecoderCore(configuration, this);

            using var bufferedStream = new BufferedReadStream(stream);
            return decoder.Identify(bufferedStream);
        }

        /// <inheritdoc/>
        public Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new GifDecoderCore(configuration, this);

            using var bufferedStream = new BufferedReadStream(stream);
            return decoder.IdentifyAsync(bufferedStream);
        }
    }
}
