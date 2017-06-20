// <copyright file="Image.FromStream.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Formats;

    using ImageSharp.PixelFormats;

    /// <content>
    /// Adds static methods allowing the creation of new image from a given stream.
    /// </content>
    public static partial class Image
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="mimeType">the mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>>
        public static Image<Rgba32> Load(Stream stream, out string mimeType) => Load<Rgba32>(stream, out mimeType);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>>
        public static Image<Rgba32> Load(Stream stream) => Load<Rgba32>(stream);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>>
        public static Image<Rgba32> Load(Stream stream, IImageDecoder decoder) => Load<Rgba32>(stream, decoder);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given stream.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>>
        public static Image<Rgba32> Load(Configuration config, Stream stream) => Load<Rgba32>(config, stream);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given stream.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="mimeType">the mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>>
        public static Image<Rgba32> Load(Configuration config, Stream stream, out string mimeType) => Load<Rgba32>(config, stream, out mimeType);

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
            return Load<TPixel>(null, stream);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="mimeType">the mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream, out string mimeType)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, stream, out mimeType);
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
            return WithSeekableStream(stream, s => decoder.Decode<TPixel>(Configuration.Default, s));
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="config">The Configuration.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Configuration config, Stream stream, IImageDecoder decoder)
            where TPixel : struct, IPixel<TPixel>
        {
            return WithSeekableStream(stream, s => decoder.Decode<TPixel>(config, s));
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Configuration config, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(config, stream, out var _);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="mimeType">the mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Configuration config, Stream stream, out string mimeType)
        where TPixel : struct, IPixel<TPixel>
        {
            config = config ?? Configuration.Default;
            mimeType = null;
            (Image<TPixel> img, IImageDecoder decoder) data = WithSeekableStream(stream, s => Decode<TPixel>(s, config));

            mimeType = data.decoder?.MimeTypes.FirstOrDefault();

            if (data.img != null)
            {
                return data.img;
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Image cannot be loaded. Available decoders:");

            foreach (IImageDecoder format in config.ImageDecoders)
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