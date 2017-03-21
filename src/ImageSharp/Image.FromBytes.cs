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
        /// Loads the image from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(byte[] data)
        {
            return Load(data, null, null);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(byte[] data, IDecoderOptions options)
        {
            return Load(data, options, null);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="config">The config for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(byte[] data, Configuration config)
        {
            return Load(data, null, config);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <param name="config">The configuration options.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(byte[] data, IDecoderOptions options, Configuration config)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load(ms, options, config);
            }
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="data">The byte array containing image data.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(byte[] data)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(data, null, null);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(byte[] data, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(data, options, null);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="config">The config for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(byte[] data, Configuration config)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(data, null, config);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <param name="config">The configuration options.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(byte[] data, IDecoderOptions options, Configuration config)
            where TColor : struct, IPixel<TColor>
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TColor>(ms, options, config);
            }
        }
    }
}
