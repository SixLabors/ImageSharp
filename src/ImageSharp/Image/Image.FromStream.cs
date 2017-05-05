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
    public partial class Image
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, stream, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, stream, options);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Configuration config, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(config, stream, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream, IImageDecoder decoder)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(stream, decoder, null);
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
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream, IImageDecoder decoder, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
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
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Configuration config, Stream stream, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            config = config ?? Configuration.Default;
            Image<TPixel> img = WithSeekableStream(stream, s => Decode<TPixel>(s, options, config));

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