// <copyright file="Image.FromStream.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.IO;
    using System.Text;
    using Formats;

    using ImageSharp.PixelFormats;

    /// <content>
    /// Adds static methods allowing the creation of new image from a given stream.
    /// </content>
    public partial class Image<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load(Stream stream)
        {
            return Load(null, stream, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load(Stream stream, IDecoderOptions options)
        {
            return Load(null, stream, options);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load(Configuration config, Stream stream)
        {
            return Load(config, stream, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load(Stream stream, IImageDecoder decoder)
        {
            return Load(stream, decoder, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load(Stream stream, IImageDecoder decoder, IDecoderOptions options)
        {
            return WithSeekableStream(stream, s => decoder.Decode<TPixel>(Configuration.Default, s, options));
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load(Configuration config, Stream stream, IDecoderOptions options)
        {
            config = config ?? Configuration.Default;
            Image<TPixel> img = WithSeekableStream(stream, s => Decode(s, options, config));

            if (img != null)
            {
                return img;
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Image cannot be loaded. Available formats:");

            foreach (IImageFormat format in config.ImageFormats)
            {
                stringBuilder.AppendLine("-" + format);
            }

            throw new NotSupportedException(stringBuilder.ToString());
        }

        private static T WithSeekableStream<T>(Stream stream, Func<Stream, T> action)
        {
            if (!stream.CanRead)
            {
                throw new NotSupportedException("Cannot read from the stream.");
            }

            if (stream.CanSeek)
            {
                return action(stream);
            }

            // We want to be able to load images from things like HttpContext.Request.Body
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ms.Position = 0;

                return action(ms);
            }
        }
    }
}