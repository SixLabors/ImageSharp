// <copyright file="Image.Decode.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Buffers;
    using System.IO;
    using System.Linq;
    using Formats;

    /// <summary>
    /// Represents an image. Each pixel is a made up four 8-bit components red, green, blue, and alpha
    /// packed into a single unsigned integer value.
    /// </summary>
    public sealed partial class Image
    {
        /// <summary>
        /// By reading the header on the provided stream this calculates the images format.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>The image format or null if none found.</returns>
        private static IImageFormat DiscoverFormat(Stream stream, Configuration config)
        {
            // This is probably a candidate for making into a public API in the future!
            int maxHeaderSize = config.MaxHeaderSize;
            if (maxHeaderSize <= 0)
            {
                return null;
            }

            IImageFormat format;
            byte[] header = ArrayPool<byte>.Shared.Rent(maxHeaderSize);
            try
            {
                long startPosition = stream.Position;
                stream.Read(header, 0, maxHeaderSize);
                stream.Position = startPosition;
                format = config.ImageFormats.FirstOrDefault(x => x.IsSupportedFileFormat(header));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }

            return format;
        }

        /// <summary>
        /// Decodes the image stream to the current image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="stream">The stream.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <param name="config">the configuration.</param>
        /// <returns>
        ///  The decoded image
        /// </returns>
        private static Image<TColor> Decode<TColor>(Stream stream, IDecoderOptions options, Configuration config)
        where TColor : struct, IPixel<TColor>
        {
            IImageFormat format = DiscoverFormat(stream, config);
            if (format == null)
            {
                return null;
            }

            Image<TColor> img = format.Decoder.Decode<TColor>(config, stream, options);
            img.CurrentImageFormat = format;
            return img;
        }
    }
}
