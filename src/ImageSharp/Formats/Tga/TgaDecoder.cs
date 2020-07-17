// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Image decoder for Truevision TGA images.
    /// </summary>
    public sealed class TgaDecoder : IImageDecoder, ITgaDecoderOptions, IImageInfoDetector
    {
        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new TgaDecoderCore(configuration, this);

            try
            {
                using var bufferedStream = new BufferedReadStream(stream);
                return decoder.Decode<TPixel>(bufferedStream);
            }
            catch (InvalidMemoryOperationException ex)
            {
                Size dims = decoder.Dimensions;

                TgaThrowHelper.ThrowInvalidImageContentException($"Cannot decode image. Failed to allocate buffers for possibly degenerate dimensions: {dims.Width}x{dims.Height}.", ex);

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
            Guard.NotNull(stream, nameof(stream));

            var decoder = new TgaDecoderCore(configuration, this);

            try
            {
                using var bufferedStream = new BufferedReadStream(stream);
                return await decoder.DecodeAsync<TPixel>(bufferedStream).ConfigureAwait(false);
            }
            catch (InvalidMemoryOperationException ex)
            {
                Size dims = decoder.Dimensions;

                TgaThrowHelper.ThrowInvalidImageContentException($"Cannot decode image. Failed to allocate buffers for possibly degenerate dimensions: {dims.Width}x{dims.Height}.", ex);

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

            using var bufferedStream = new BufferedReadStream(stream);
            return new TgaDecoderCore(configuration, this).Identify(bufferedStream);
        }

        /// <inheritdoc/>
        public Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            using var bufferedStream = new BufferedReadStream(stream);
            return new TgaDecoderCore(configuration, this).IdentifyAsync(bufferedStream);
        }
    }
}
