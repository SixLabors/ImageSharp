// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Encoder for generating an image out of a png encoded stream.
    /// </summary>
    /// <remarks>
    /// At the moment the following features are supported:
    /// <para>
    /// <b>Filters:</b> all filters are supported.
    /// </para>
    /// <para>
    /// <b>Pixel formats:</b>
    /// <list type="bullet">
    ///     <item>RGBA (True color) with alpha (8 bit).</item>
    ///     <item>RGB (True color) without alpha (8 bit).</item>
    ///     <item>grayscale with alpha (8 bit).</item>
    ///     <item>grayscale without alpha (8 bit).</item>
    ///     <item>Palette Index with alpha (8 bit).</item>
    ///     <item>Palette Index without alpha (8 bit).</item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class PngDecoder : IImageDecoder, IPngDecoderOptions, IImageInfoDetector
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The decoded image.</returns>
        public async Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new PngDecoderCore(configuration, this);

            try
            {
                return await decoder.DecodeAsync<TPixel>(stream).ConfigureAwait(false);
            }
            catch (InvalidMemoryOperationException ex)
            {
                Size dims = decoder.Dimensions;

                PngThrowHelper.ThrowInvalidImageContentException($"Can not decode image. Failed to allocate buffers for possibly degenerate dimensions: {dims.Width}x{dims.Height}.", ex);

                // Not reachable, as the previous statement will throw a exception.
                return null;
            }
        }

        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decoder = new PngDecoderCore(configuration, this);

            try
            {
                return decoder.Decode<TPixel>(stream);
            }
            catch (InvalidMemoryOperationException ex)
            {
                Size dims = decoder.Dimensions;

                PngThrowHelper.ThrowInvalidImageContentException($"Can not decode image. Failed to allocate buffers for possibly degenerate dimensions: {dims.Width}x{dims.Height}.", ex);

                // Not reachable, as the previous statement will throw a exception.
                return null;
            }
        }

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            var decoder = new PngDecoderCore(configuration, this);
            return decoder.Identify(stream);
        }

        /// <inheritdoc/>
        public async Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream)
        {
            var decoder = new PngDecoderCore(configuration, this);
            return await decoder.IdentifyAsync(stream).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream) => this.Decode<Rgba32>(configuration, stream);

        /// <inheritdoc />
        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream) => await this.DecodeAsync<Rgba32>(configuration, stream).ConfigureAwait(false);
    }
}
