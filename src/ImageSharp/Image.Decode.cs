// <copyright file="Image.Decode.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Formats;

    /// <summary>
    /// Represents an image. Each pixel is a made up four 8-bit components red, green, blue, and alpha
    /// packed into a single unsigned integer value.
    /// </summary>
    public sealed partial class Image
    {
        /// <summary>
        /// Decodes the image stream to the current image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="stream">The stream.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <param name="config">the configuration.</param>
        /// <param name="img">The decoded image</param>
        /// <returns>
        ///  [true] if can successfull decode the image otherwise [false].
        /// </returns>
        private static bool Decode<TColor>(Stream stream, IDecoderOptions options, Configuration config, out Image<TColor> img)
            where TColor : struct, IPixel<TColor>
        {
            img = null;
            int maxHeaderSize = config.MaxHeaderSize;
            if (maxHeaderSize <= 0)
            {
                return false;
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

            if (format == null)
            {
                return false;
            }

            img = format.Decoder.Decode<TColor>(stream, options);
            img.CurrentImageFormat = format;
            return true;
        }
    }
}
