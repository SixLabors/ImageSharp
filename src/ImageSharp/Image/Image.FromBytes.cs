// <copyright file="Image.FromBytes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.IO;
    using Formats;

    using ImageSharp.PixelFormats;

    /// <content>
    /// Adds static methods allowing the creation of new image from a byte array.
    /// </content>
    public sealed partial class Image
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data) => Load<Rgba32>(null, data, null);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data, IDecoderOptions options) => Load<Rgba32>(null, data, options);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data) => Load<Rgba32>(config, data, null);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data, IImageDecoder decoder) => Load<Rgba32>(data, decoder, null);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data, IDecoderOptions options) => Load<Rgba32>(config, data, options);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data, IImageDecoder decoder, IDecoderOptions options) => Load<Rgba32>(data, decoder, options);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, data, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, data, options);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, byte[] data)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(config, data, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data, IImageDecoder decoder)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(data, decoder, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, byte[] data, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TPixel>(config, ms, options);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data, IImageDecoder decoder, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TPixel>(ms, decoder, options);
            }
        }
    }
}