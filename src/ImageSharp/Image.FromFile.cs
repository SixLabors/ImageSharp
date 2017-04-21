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

    /// <summary>
    /// Represents an image. Each pixel is a made up four 8-bit components red, green, blue, and alpha
    /// packed into a single unsigned integer value.
    /// </summary>
    public sealed partial class Image
    {
        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(string path)
        {
            return Load(null, path, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(string path, IDecoderOptions options)
        {
            return Load(null, path, options);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(Configuration config, string path)
        {
            return Load(config, path, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(string path, IImageDecoder decoder)
        {
            return Load(path, decoder, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(string path, IImageDecoder decoder, IDecoderOptions options)
        {
            return new Image(Load<Rgba32>(path, decoder, options));
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(Configuration config, string path, IDecoderOptions options)
        {
            config = config ?? Configuration.Default;
            using (Stream s = config.FileSystem.OpenRead(path))
            {
                return Load(config, s, options);
            }
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load<TPixel>(string path)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, path, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load<TPixel>(string path, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, path, options);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, string path)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(config, path, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TPixel> Load<TPixel>(string path, IImageDecoder decoder)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(path, decoder, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="config">The configuration options.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
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
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
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
