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
    public static partial class Image
    {
        /// <summary>
        /// By reading the header on the provided byte array this calculates the images mimetype.
        /// </summary>
        /// <param name="data">The byte array containing image data to read the header from.</param>
        /// <returns>The mimetype or null if none found.</returns>
        public static string DiscoverMimeType(byte[] data)
        {
            return DiscoverMimeType(null, data);
        }

        /// <summary>
        /// By reading the header on the provided byte array this calculates the images mimetype.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="data">The byte array containing image data to read the header from.</param>
        /// <returns>The mimetype or null if none found.</returns>
        public static string DiscoverMimeType(Configuration config, byte[] data)
        {
            using (Stream stream = new MemoryStream(data))
            {
                return DiscoverMimeType(config, stream);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data) => Load<Rgba32>(null, data);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="mimeType">the mime type of the decoded image.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data, out string mimeType) => Load<Rgba32>(null, data, out mimeType);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data) => Load<Rgba32>(config, data);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="mimeType">the mime type of the decoded image.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data, out string mimeType) => Load<Rgba32>(config, data, out mimeType);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data, IImageDecoder decoder) => Load<Rgba32>(data, decoder);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data, IImageDecoder decoder) => Load<Rgba32>(config, data, decoder);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, data);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="mimeType">the mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data, out string mimeType)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, data, out mimeType);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, byte[] data)
            where TPixel : struct, IPixel<TPixel>
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TPixel>(config, ms);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="mimeType">the mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, byte[] data, out string mimeType)
            where TPixel : struct, IPixel<TPixel>
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TPixel>(config, ms, out mimeType);
            }
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
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TPixel>(ms, decoder);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The Configuration.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, byte[] data, IImageDecoder decoder)
            where TPixel : struct, IPixel<TPixel>
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return Load<TPixel>(config, ms, decoder);
            }
        }
    }
}