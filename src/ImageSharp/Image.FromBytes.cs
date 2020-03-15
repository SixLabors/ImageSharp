// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing the creation of new image from a byte array.
    /// </content>
    public abstract partial class Image
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
        /// <param name="configuration">The configuration.</param>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(Configuration configuration, byte[] data)
        {
            Guard.NotNull(configuration, nameof(configuration));
            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return DetectFormat(configuration, stream);
            }
        }

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <exception cref="NotSupportedException">Thrown if the stream is not readable.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        public static IImageInfo Identify(byte[] data) => Identify(data, out IImageFormat _);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="NotSupportedException">Thrown if the stream is not readable.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        public static IImageInfo Identify(byte[] data, out IImageFormat format) => Identify(Configuration.Default, data, out format);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="NotSupportedException">Thrown if the stream is not readable.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector is not found.
        /// </returns>
        public static IImageInfo Identify(Configuration configuration, byte[] data, out IImageFormat format)
        {
            Guard.NotNull(configuration, nameof(configuration));
            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return Identify(configuration, stream, out format);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{Rgba32}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        public static Image<Rgba32> Load(byte[] data) => Load<Rgba32>(Configuration.Default, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(byte[] data, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, byte[] data)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return Load<TPixel>(configuration, stream);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, byte[] data, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return Load<TPixel>(configuration, stream, out format);
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
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return Load<TPixel>(stream, decoder);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, byte[] data, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return Load<TPixel>(configuration, stream, decoder);
            }
        }

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
        /// <param name="configuration">The configuration.</param>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(Configuration configuration, ReadOnlySpan<byte> data)
        {
            int maxHeaderSize = configuration.MaxHeaderSize;
            if (maxHeaderSize <= 0)
            {
                return null;
            }

            foreach (IImageFormatDetector detector in configuration.ImageFormatsManager.FormatDetectors)
            {
                IImageFormat f = detector.DetectFormat(data);

                if (f != null)
                {
                    return f;
                }
            }

            return default;
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="data">The byte span containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(ReadOnlySpan<byte> data)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(ReadOnlySpan<byte> data, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte span containing encoded image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(ReadOnlySpan<byte> data, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data, decoder);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte span containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static unsafe Image<TPixel> Load<TPixel>(Configuration configuration, ReadOnlySpan<byte> data)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load<TPixel>(configuration, stream);
                }
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static unsafe Image<TPixel> Load<TPixel>(
            Configuration configuration,
            ReadOnlySpan<byte> data,
            IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load<TPixel>(configuration, stream, decoder);
                }
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static unsafe Image<TPixel> Load<TPixel>(
            Configuration configuration,
            ReadOnlySpan<byte> data,
            out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load<TPixel>(configuration, stream, out format);
                }
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The detected format.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(byte[] data, out IImageFormat format) =>
            Load(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(byte[] data, IImageDecoder decoder) => Load(Configuration.Default, data, decoder);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(Configuration configuration, byte[] data) => Load(configuration, data, out _);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(Configuration configuration, byte[] data, IImageDecoder decoder)
        {
            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return Load(configuration, stream, decoder);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(Configuration configuration, byte[] data, out IImageFormat format)
        {
            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return Load(configuration, stream, out format);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="data">The byte span containing image data.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(ReadOnlySpan<byte> data) => Load(Configuration.Default, data);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(ReadOnlySpan<byte> data, IImageDecoder decoder) =>
            Load(Configuration.Default, data, decoder);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The detected format.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(ReadOnlySpan<byte> data, out IImageFormat format) =>
            Load(Configuration.Default, data, out format);

        /// <summary>
        /// Decodes a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(Configuration configuration, ReadOnlySpan<byte> data) => Load(configuration, data, out _);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="decoder">The decoder.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static unsafe Image Load(
            Configuration configuration,
            ReadOnlySpan<byte> data,
            IImageDecoder decoder)
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load(configuration, stream, decoder);
                }
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>>
        /// <returns>The <see cref="Image"/>.</returns>
        public static unsafe Image Load(
            Configuration configuration,
            ReadOnlySpan<byte> data,
            out IImageFormat format)
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load(configuration, stream, out format);
                }
            }
        }
    }
}
