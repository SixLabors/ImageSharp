// <copyright file="Image.FromStream.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
#if !NETSTANDARD1_1
    using System;
    using System.IO;
    using Formats;
    using ImageSharp.PixelFormats;

    /// <content>
    /// Adds static methods allowing the creation of new image from a given file.
    /// </content>
    public partial class Image
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(string path)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, path, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(string path, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, path, options);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, string path)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(config, path, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(string path, IImageDecoder decoder)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(path, decoder, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, string path, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            config = config ?? Configuration.Default;
            using (Stream s = config.FileSystem.OpenRead(path))
            {
                return Load<TPixel>(config, s, options);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(string path, IImageDecoder decoder, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            Configuration config = Configuration.Default;
            using (Stream s = config.FileSystem.OpenRead(path))
            {
                return Load<TPixel>(s, decoder, options);
            }
        }
    }
#endif
}