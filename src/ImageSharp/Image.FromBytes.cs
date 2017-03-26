// <copyright file="Image.FromStream.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.IO;
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
            return Load(null, data, null);
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
            return Load(null, data, options);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(Configuration config, byte[] data)
        {
            return Load(config, data, null);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(byte[] data, IImageDecoder decoder)
        {
            return Load(data, decoder, null);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(Configuration config, byte[] data, IDecoderOptions options)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load(config, ms, options);
            }
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image Load(byte[] data, IImageDecoder decoder, IDecoderOptions options)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load(ms, decoder, options);
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
            return Load<TColor>(null, data, null);
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
            return Load<TColor>(null, data, options);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(Configuration config, byte[] data)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(config, data, null);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(byte[] data, IImageDecoder decoder)
            where TColor : struct, IPixel<TColor>
        {
            return Load<TColor>(data, decoder, null);
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(Configuration config, byte[] data, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TColor>(config, ms, options);
            }
        }

        /// <summary>
        /// Loads the image from the given byte array.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>The image</returns>
        public static Image<TColor> Load<TColor>(byte[] data, IImageDecoder decoder, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TColor>(ms, decoder, options);
            }
        }
    }
}
