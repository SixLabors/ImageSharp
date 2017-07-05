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
        /// By reading the header on the provided byte array this calculates the images format.
        /// </summary>
        /// <param name="data">The byte array containing image data to read the header from.</param>
        /// <returns>The format or null if none found.</returns>
        public static IImageFormat DetectFormat(byte[] data)
        {
            return DetectFormat(null, data);
        }

        /// <summary>
        /// By reading the header on the provided byte array this calculates the images format.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="data">The byte array containing image data to read the header from.</param>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(Configuration config, byte[] data)
        {
            using (Stream stream = new MemoryStream(data))
            {
                return DetectFormat(config, stream);
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
        /// <param name="format">The mime type of the decoded image.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data, out IImageFormat format) => Load<Rgba32>(null, data, out format);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data) => Load<Rgba32>(config, data);

        /// <summary>
        /// Create a new instance of the <see cref="Image{Rgba32}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data, out IImageFormat format) => Load<Rgba32>(config, data, out format);

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
        /// <param name="format">The mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data, out IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
        {
            return Load<TPixel>(null, data, out format);
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
            using (var memoryStream = new MemoryStream(data))
            {
                return Load<TPixel>(config, memoryStream);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration config, byte[] data, out IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var memoryStream = new MemoryStream(data))
            {
                return Load<TPixel>(config, memoryStream, out format);
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
            using (var memoryStream = new MemoryStream(data))
            {
                return Load<TPixel>(memoryStream, decoder);
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
            using (var memoryStream = new MemoryStream(data))
            {
                return Load<TPixel>(config, memoryStream, decoder);
            }
        }
    }
}