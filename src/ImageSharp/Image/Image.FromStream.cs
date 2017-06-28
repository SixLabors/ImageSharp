// <copyright file="Image.FromStream.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Formats;

    using ImageSharp.PixelFormats;

    /// <content>
    /// Adds static methods allowing the creation of new image from a given stream.
    /// </content>
    public static partial class Image
    {
        /// <summary>
        /// By reading the header on the provided stream this calculates the images mime type.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(Stream stream)
        {
            return DetectFormat(null, stream);
        }

        /// <summary>
        /// By reading the header on the provided stream this calculates the images mime type.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(Configuration config, Stream stream)
        {
            return WithSeekableStream(stream, s => InternalDetectFormat(s, config ?? Configuration.Default));
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="format">the mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>>
        public static Image<Rgba32> Load(Stream stream, out IImageFormat format) => Load<Rgba32>(stream, out format);

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
        /// <param name="format">the mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>>
        public static Image<Rgba32> Load(Configuration config, Stream stream, out IImageFormat format) => Load<Rgba32>(config, stream, out format);

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
        /// <param name="format">the mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream, out IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, stream, out format);
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
        /// <param name="format">the mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Configuration config, Stream stream, out IImageFormat format)
        where TPixel : struct, IPixel<TPixel>
        {
            config = config ?? Configuration.Default;
            (Image<TPixel> img, IImageFormat format) data = WithSeekableStream(stream, s => Decode<TPixel>(s, config));

            format = data.format;

            if (data.img != null)
            {
                return data.img;
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Image cannot be loaded. Available decoders:");

            foreach (KeyValuePair<IImageFormat, IImageDecoder> val in config.ImageDecoders)
            {
                stringBuilder.AppendLine($" - {val.Key.Name} : {val.Value.GetType().Name}");
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
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                return action(memoryStream);
            }
        }
    }
}