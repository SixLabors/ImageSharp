// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing the decoding of new images.
    /// </content>
    public abstract partial class Image
    {
        /// <summary>
        /// Creates an <see cref="Image{TPixel}"/> instance backed by an uninitialized memory buffer.
        /// This is an optimized creation method intended to be used by decoders.
        /// The image might be filled with memory garbage.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type</typeparam>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="metadata">The <see cref="ImageMetadata"/></param>
        /// <returns>The result <see cref="Image{TPixel}"/></returns>
        internal static Image<TPixel> CreateUninitialized<TPixel>(
            Configuration configuration,
            int width,
            int height,
            ImageMetadata metadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Buffer2D<TPixel> uninitializedMemoryBuffer = configuration.MemoryAllocator.Allocate2D<TPixel>(
                    width,
                    height,
                    configuration.PreferContiguousImageBuffers);
            return new Image<TPixel>(configuration, uninitializedMemoryBuffer.FastMemoryGroup, width, height, metadata);
        }

        /// <summary>
        /// By reading the header on the provided stream this calculates the images format.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>The mime type or null if none found.</returns>
        private static IImageFormat InternalDetectFormat(Stream stream, Configuration config)
        {
            // We take a minimum of the stream length vs the max header size and always check below
            // to ensure that only formats that headers fit within the given buffer length are tested.
            int headerSize = (int)Math.Min(config.MaxHeaderSize, stream.Length);
            if (headerSize <= 0)
            {
                return null;
            }

            // Header sizes are so small, that headersBuffer will be always stackalloc-ed in practice,
            // and heap allocation will never happen, there is no need for the usual try-finally ArrayPool dance.
            // The array case is only a safety mechanism following stackalloc best practices.
            Span<byte> headersBuffer = headerSize > 512 ? new byte[headerSize] : stackalloc byte[headerSize];
            long startPosition = stream.Position;

            // Read doesn't always guarantee the full returned length so read a byte
            // at a time until we get either our count or hit the end of the stream.
            int n = 0;
            int i;
            do
            {
                i = stream.Read(headersBuffer, n, headerSize - n);
                n += i;
            }
            while (n < headerSize && i > 0);

            stream.Position = startPosition;

            // Does the given stream contain enough data to fit in the header for the format
            // and does that data match the format specification?
            // Individual formats should still check since they are public.
            IImageFormat format = null;
            foreach (IImageFormatDetector formatDetector in config.ImageFormatsManager.FormatDetectors)
            {
                if (formatDetector.HeaderSize <= headerSize)
                {
                    IImageFormat attemptFormat = formatDetector.DetectFormat(headersBuffer);
                    if (attemptFormat != null)
                    {
                        format = attemptFormat;
                    }
                }
            }

            return format;
        }

        /// <summary>
        /// By reading the header on the provided stream this calculates the images format.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="format">The IImageFormat.</param>
        /// <returns>The image format or null if none found.</returns>
        private static IImageDecoder DiscoverDecoder(Stream stream, Configuration config, out IImageFormat format)
        {
            format = InternalDetectFormat(stream, config);

            return format != null
                ? config.ImageFormatsManager.FindDecoder(format)
                : null;
        }

        /// <summary>
        /// Decodes the image stream to the current image.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="config">the configuration.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>
        /// A new <see cref="Image{TPixel}"/>.
        /// </returns>
        private static (Image<TPixel> Image, IImageFormat Format) Decode<TPixel>(Stream stream, Configuration config, CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IImageDecoder decoder = DiscoverDecoder(stream, config, out IImageFormat format);
            if (decoder is null)
            {
                return (null, null);
            }

            Image<TPixel> img = decoder.Decode<TPixel>(config, stream, cancellationToken);
            return (img, format);
        }

        private static (Image Image, IImageFormat Format) Decode(Stream stream, Configuration config, CancellationToken cancellationToken = default)
        {
            IImageDecoder decoder = DiscoverDecoder(stream, config, out IImageFormat format);
            if (decoder is null)
            {
                return (null, null);
            }

            Image img = decoder.Decode(config, stream, cancellationToken);
            return (img, format);
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="config">the configuration.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if a suitable info detector is not found.
        /// </returns>
        private static (IImageInfo ImageInfo, IImageFormat Format) InternalIdentity(Stream stream, Configuration config, CancellationToken cancellationToken = default)
        {
            IImageDecoder decoder = DiscoverDecoder(stream, config, out IImageFormat format);

            if (decoder is not IImageInfoDetector detector)
            {
                return (null, null);
            }

            IImageInfo info = detector?.Identify(config, stream, cancellationToken);
            return (info, format);
        }
    }
}
