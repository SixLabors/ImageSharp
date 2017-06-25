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

    using ImageSharp.PixelFormats;

    /// <content>
    /// Adds static methods allowing the decoding of new images.
    /// </content>
    public static partial class Image
    {
        /// <summary>
        /// By reading the header on the provided stream this calculates the images format.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>The mime type or null if none found.</returns>
        private static string InternalDiscoverMimeType(Stream stream, Configuration config)
        {
            // This is probably a candidate for making into a public API in the future!
            int maxHeaderSize = config.MaxHeaderSize;
            if (maxHeaderSize <= 0)
            {
                return null;
            }

            byte[] header = ArrayPool<byte>.Shared.Rent(maxHeaderSize);
            try
            {
                long startPosition = stream.Position;
                stream.Read(header, 0, maxHeaderSize);
                stream.Position = startPosition;
                return config.MimeTypeDetectors.Select(x => x.DetectMimeType(header)).LastOrDefault(x => x != null);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }
        }

        /// <summary>
        /// By reading the header on the provided stream this calculates the images format.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="mimeType">The mimeType.</param>
        /// <returns>The image format or null if none found.</returns>
        private static IImageDecoder DiscoverDecoder(Stream stream, Configuration config, out string mimeType)
        {
            mimeType = InternalDiscoverMimeType(stream, config);
            if (mimeType != null)
            {
                return config.FindMimeTypeDecoder(mimeType);
            }

            return null;
        }

#pragma warning disable SA1008 // Opening parenthesis must be spaced correctly
        /// <summary>
        /// Decodes the image stream to the current image.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="config">the configuration.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>
        /// A new <see cref="Image{TPixel}"/>.
        /// </returns>
        private static (Image<TPixel> img, string mimeType) Decode<TPixel>(Stream stream, Configuration config)
#pragma warning restore SA1008 // Opening parenthesis must be spaced correctly
            where TPixel : struct, IPixel<TPixel>
        {
            IImageDecoder decoder = DiscoverDecoder(stream, config, out string mimeType);
            if (decoder == null)
            {
                return (null, null);
            }

            Image<TPixel> img = decoder.Decode<TPixel>(config, stream);
            return (img, mimeType);
        }
    }
}