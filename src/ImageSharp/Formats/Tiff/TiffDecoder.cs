// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Image decoder for generating an image out of a TIFF stream.
    /// </summary>
    public class TiffDecoder : IImageDecoder, ITiffDecoderOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, "stream");

            using (TiffDecoderCore decoder = new TiffDecoderCore(stream, configuration, this))
            {
                return decoder.Decode<TPixel>();
            }
        }

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="T:SixLabors.ImageSharp.Image" />.
        /// The decoder is free to choose the pixel type.
        /// </summary>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="T:System.IO.Stream" /> containing image data.</param>
        /// <returns>
        /// The decoded image of a pixel type chosen by the decoder.
        /// </returns>
        public Image Decode(Configuration configuration, Stream stream) => this.Decode<Rgba32>(configuration, stream);

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image"/>.
        /// </summary>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public Task<Image> DecodeAsync(Configuration configuration, Stream stream)
        {
            throw new System.NotImplementedException();
        }
    }
}
