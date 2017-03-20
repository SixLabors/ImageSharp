// <copyright file="Image.FromStream.cs" company="James Jackson-South">
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
        /// Loads the image from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(Stream stream)
        {
            return Load(stream, null, null);
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(Stream stream, IDecoderOptions options)
        {
            return Load(stream, options, null);
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="config">The config for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(Stream stream, Configuration config)
        {
            return Load(stream, null, config);
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <param name="config">The configuration options.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(Stream stream, IDecoderOptions options, Configuration config)
        {
            return new Image(Load<Color>(stream, options, config));
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(Stream stream)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(stream, null, null);
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(Stream stream, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(stream, options, null);
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="config">The config for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(Stream stream, Configuration config)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(stream, null, config);
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <param name="config">The configuration options.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(Stream stream, IDecoderOptions options, Configuration config)
            where TColor : struct, IPixel<TColor>
        {
            config = config ?? Configuration.Default;

            if (!config.ImageFormats.Any())
            {
                throw new InvalidOperationException("No image formats have been configured.");
            }

            if (!stream.CanRead)
            {
                throw new NotSupportedException("Cannot read from the stream.");
            }

            if (stream.CanSeek)
            {
                if (Decode(stream, options, config, out Image<TColor> img))
                {
                    return img;
                }
            }
            else
            {
                // We want to be able to load images from things like HttpContext.Request.Body
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;

                    if (Decode(ms, options, config, out Image<TColor> img))
                    {
                        return img;
                    }
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Image cannot be loaded. Available formats:");

            foreach (IImageFormat format in config.ImageFormats)
            {
                stringBuilder.AppendLine("-" + format);
            }

            throw new NotSupportedException(stringBuilder.ToString());
        }
    }
}
