// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
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
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            using (var decoder = new JpegDecoderCore(configuration, this))
            {
                return decoder.Decode<TPixel>(stream);
            }
        }

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            using (var decoder = new JpegDecoderCore(configuration, this))
            {
                return decoder.Identify(stream);
            }
        }
    }
}