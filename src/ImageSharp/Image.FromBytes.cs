// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing the creation of new image from a byte array.
    /// </content>
    public static partial class Image
    {
        /// <summary>
        /// By reading the header on the provided byte array this calculates the images format.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <returns>The format or null if none found.</returns>
        public static IImageFormat DetectFormat(byte[] data)
        {
            return DetectFormat(Configuration.Default, data);
        }

        /// <summary>
        /// By reading the header on the provided byte array this calculates the images format.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(Configuration config, byte[] data)
        {
            using (Stream stream = new MemoryStream(data))
            {
                return DetectFormat(config, stream);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data) => Load<Rgba32>(Configuration.Default, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data, out IImageFormat format) => Load<Rgba32>(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data) => Load<Rgba32>(config, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data, out IImageFormat format) => Load<Rgba32>(config, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data, IImageDecoder decoder) => Load<Rgba32>(data, decoder);

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, byte[] data, IImageDecoder decoder) => Load<Rgba32>(config, data, decoder);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data)
            where TPixel : struct, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data, out IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
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
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>
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
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data.</param>
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
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="config">The Configuration.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
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

#if !NETSTANDARD1_1

        /// <summary>
        /// By reading the header on the provided byte array this calculates the images format.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <returns>The format or null if none found.</returns>
        public static IImageFormat DetectFormat(ReadOnlySpan<byte> data)
        {
            return DetectFormat(Configuration.Default, data);
        }

        /// <summary>
        /// By reading the header on the provided byte array this calculates the images format.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <returns>The mime type or null if none found.</returns>
        public static unsafe IImageFormat DetectFormat(Configuration config, ReadOnlySpan<byte> data)
        {
            int maxHeaderSize = config.MaxHeaderSize;
            if (maxHeaderSize <= 0)
            {
                return null;
            }

            IImageFormat format = default;
            foreach (IImageFormatDetector detector in config.ImageFormatsManager.FormatDetectors)
            {
                IImageFormat f = detector.DetectFormat(data);

                if (f != null)
                {
                    format = f;
                }
            }

            return format;
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="data">The byte span containing image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(ReadOnlySpan<byte> data) => Load<Rgba32>(Configuration.Default, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="config">The config for the decoder.</param>
        /// <param name="data">The byte span containing encoded image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(Configuration config, ReadOnlySpan<byte> data) => Load<Rgba32>(config, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="data">The byte span containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(ReadOnlySpan<byte> data)
            where TPixel : struct, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte span containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static unsafe Image<TPixel> Load<TPixel>(Configuration config, ReadOnlySpan<byte> data)
            where TPixel : struct, IPixel<TPixel>
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load<TPixel>(config, stream);
                }
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="config">The Configuration.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static unsafe Image<TPixel> Load<TPixel>(
            Configuration config,
            ReadOnlySpan<byte> data,
            IImageDecoder decoder)
            where TPixel : struct, IPixel<TPixel>
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load<TPixel>(config, stream, decoder);
                }
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="config">The configuration options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static unsafe Image<TPixel> Load<TPixel>(
            Configuration config,
            ReadOnlySpan<byte> data,
            out IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load<TPixel>(config, stream, out format);
                }
            }
        }
#endif
    }
}