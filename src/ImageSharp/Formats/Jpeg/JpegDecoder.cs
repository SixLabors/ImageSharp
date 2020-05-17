// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Image decoder for generating an image out of a jpg stream.
    /// </summary>
    public sealed class JpegDecoder : IImageDecoder, IJpegDecoderOptions, IImageInfoDetector
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public async Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            using var decoder = new JpegDecoderCore(configuration, this);
            try
            {
                return await decoder.DecodeAsync<TPixel>(stream).ConfigureAwait(false);
            }
            catch (InvalidMemoryOperationException ex)
            {
                (int w, int h) = (decoder.ImageWidth, decoder.ImageHeight);

                JpegThrowHelper.ThrowInvalidImageContentException($"Can not decode image. Failed to allocate buffers for possibly degenerate dimensions: {w}x{h}.", ex);

                // Not reachable, as the previous statement will throw a exception.
                return null;
            }
        }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            using var decoder = new JpegDecoderCore(configuration, this);
            try
            {
                return decoder.Decode<TPixel>(stream);
            }
            catch (InvalidMemoryOperationException ex)
            {
                (int w, int h) = (decoder.ImageWidth, decoder.ImageHeight);

                JpegThrowHelper.ThrowInvalidImageContentException($"Can not decode image. Failed to allocate buffers for possibly degenerate dimensions: {w}x{h}.", ex);

                // Not reachable, as the previous statement will throw a exception.
                return null;
            }
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream)
            => this.Decode<Rgba32>(configuration, stream);

        /// <inheritdoc />
        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream)
            => await this.DecodeAsync<Rgba32>(configuration, stream).ConfigureAwait(false);

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            using (var decoder = new JpegDecoderCore(configuration, this))
            {
                return decoder.Identify(stream);
            }
        }

        /// <inheritdoc/>
        public async Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            using (var decoder = new JpegDecoderCore(configuration, this))
            {
                return await decoder.IdentifyAsync(stream).ConfigureAwait(false);
            }
        }
    }
}
