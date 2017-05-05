// <copyright file="Image{TPixel}.FromBytes.cs" company="James Jackson-South">
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
    public partial class Image<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load(byte[] data)
        {
            return Load(null, data, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load(byte[] data, IDecoderOptions options)
        {
            return Load(null, data, options);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load(Configuration config, byte[] data)
        {
            return Load(config, data, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load(byte[] data, IImageDecoder decoder)
        {
            return Load(data, decoder, null);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load(Configuration config, byte[] data, IDecoderOptions options)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load(config, ms, options);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load(byte[] data, IImageDecoder decoder, IDecoderOptions options)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load(ms, decoder, options);
            }
        }
    }
}