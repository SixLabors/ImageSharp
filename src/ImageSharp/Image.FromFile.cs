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

#if !NETSTANDARD1_1
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
            return Load(path, null, (Configuration)null);
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
            return Load(path, options, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="config">The config for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(string path, Configuration config)
        {
            return Load(path, null, config);
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
            return new Image(Load<Color>(path, decoder, options));
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <param name="config">The configuration options.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(string path, IDecoderOptions options, Configuration config)
        {
            config = config ?? Configuration.Default;
            using (Stream s = config.FileSystem.OpenRead(path))
            {
                return Load(s, options, config);
            }
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(string path)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(path, null, (Configuration)null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(string path, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(path, options, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <param name="config">The config for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(string path, Configuration config)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(path, null, config);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(string path, IImageDecoder decoder)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(path, decoder, null);
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <param name="config">The configuration options.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(string path, IDecoderOptions options, Configuration config)
            where TColor : struct, IPixel<TColor>
        {
            config = config ?? Configuration.Default;
            using (Stream s = config.FileSystem.OpenRead(path))
            {
                return Load<TColor>(s, options, config);
            }
        }

        /// <summary>
        /// Loads the image from the given file.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(string path, IImageDecoder decoder, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            Configuration config = Configuration.Default;
            using (Stream s = config.FileSystem.OpenRead(path))
            {
                return Load<TColor>(s, decoder, options);
            }
        }
    }
#endif
}
