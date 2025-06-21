// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

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
        return new(configuration, uninitializedMemoryBuffer.FastMemoryGroup, width, height, metadata);
    }

    /// <summary>
    /// By reading the header on the provided stream this calculates the images format.
    /// </summary>
    /// <param name="configuration">The general configuration.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <returns>The mime type or null if none found.</returns>
    /// <exception cref="UnknownImageFormatException">The input format is not recognized.</exception>
    private static IImageFormat InternalDetectFormat(Configuration configuration, Stream stream)
    {
        // We take a minimum of the stream length vs the max header size and always check below
        // to ensure that only formats that headers fit within the given buffer length are tested.
        int headerSize = (int)Math.Min(configuration.MaxHeaderSize, stream.Length);
        if (headerSize <= 0)
        {
            ImageFormatManager.ThrowInvalidDecoder(configuration.ImageFormatsManager);
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
            i = stream.Read(headersBuffer[n..headerSize]);
            n += i;
        }
        while (n < headerSize && i > 0);

        stream.Position = startPosition;

        return InternalDetectFormat(configuration, headersBuffer[..n]);
    }

    /// <summary>
    /// By reading the header on the provided stream this calculates the images format.
    /// </summary>
    /// <param name="configuration">The general configuration.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The mime type or null if none found.</returns>
    /// <exception cref="UnknownImageFormatException">The input format is not recognized.</exception>
    private static async ValueTask<IImageFormat> InternalDetectFormatAsync(
        Configuration configuration,
        Stream stream,
        CancellationToken cancellationToken)
    {
        // We take a minimum of the stream length vs the max header size and always check below
        // to ensure that only formats that headers fit within the given buffer length are tested.
        int headerSize = (int)Math.Min(configuration.MaxHeaderSize, stream.Length);
        if (headerSize <= 0)
        {
            ImageFormatManager.ThrowInvalidDecoder(configuration.ImageFormatsManager);
        }

        using (IMemoryOwner<byte> memoryOwner = configuration.MemoryAllocator.Allocate<byte>(headerSize))
        {
            Memory<byte> headersBuffer = memoryOwner.Memory;
            long startPosition = stream.Position;

            // Read doesn't always guarantee the full returned length so read a byte
            // at a time until we get either our count or hit the end of the stream.
            int n = 0;
            int i;
            do
            {
                i = await stream.ReadAsync(headersBuffer[n..headerSize], cancellationToken);
                n += i;
            }
            while (n < headerSize && i > 0);

            stream.Position = startPosition;

            return InternalDetectFormat(configuration, headersBuffer.Span[..n]);
        }
    }

    private static IImageFormat InternalDetectFormat(
        Configuration configuration,
        ReadOnlySpan<byte> headersBuffer)
    {
        // Does the given stream contain enough data to fit in the header for the format
        // and does that data match the format specification?
        // Individual formats should still check since they are public.
        foreach (IImageFormatDetector formatDetector in configuration.ImageFormatsManager.FormatDetectors)
        {
            if (formatDetector.HeaderSize <= headersBuffer.Length && formatDetector.TryDetectFormat(headersBuffer, out IImageFormat? attemptFormat))
            {
                return attemptFormat;
            }
        }

        ImageFormatManager.ThrowInvalidDecoder(configuration.ImageFormatsManager);

        // Need to write this otherwise compiler is not happy
        return null;
    }

    /// <summary>
    /// By reading the header on the provided stream this calculates the images format.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <returns>The <see cref="IImageDecoder"/>.</returns>
    private static IImageDecoder DiscoverDecoder(DecoderOptions options, Stream stream)
    {
        IImageFormat format = InternalDetectFormat(options.Configuration, stream);
        return options.Configuration.ImageFormatsManager.GetDecoder(format);
    }

    /// <summary>
    /// By reading the header on the provided stream this calculates the images format.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IImageDecoder"/>.</returns>
    private static async ValueTask<IImageDecoder> DiscoverDecoderAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken)
    {
        IImageFormat format = await InternalDetectFormatAsync(options.Configuration, stream, cancellationToken);
        return options.Configuration.ImageFormatsManager.GetDecoder(format);
    }

    /// <summary>
    /// Decodes the image stream to the current image.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>
    /// A new <see cref="Image{TPixel}"/>.
    /// </returns>
    private static Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IImageDecoder decoder = DiscoverDecoder(options, stream);
        return decoder.Decode<TPixel>(options, stream);
    }

    private static async Task<Image<TPixel>> DecodeAsync<TPixel>(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IImageDecoder decoder = await DiscoverDecoderAsync(options, stream, cancellationToken);
        return await decoder.DecodeAsync<TPixel>(options, stream, cancellationToken);
    }

    private static Image Decode(DecoderOptions options, Stream stream)
    {
        IImageDecoder decoder = DiscoverDecoder(options, stream);
        return decoder.Decode(options, stream);
    }

    private static async Task<Image> DecodeAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken)
    {
        IImageDecoder decoder = await DiscoverDecoderAsync(options, stream, cancellationToken);
        return await decoder.DecodeAsync(options, stream, cancellationToken);
    }

    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream.</param>
    /// <returns>The <see cref="ImageInfo"/>.</returns>
    private static ImageInfo InternalIdentify(DecoderOptions options, Stream stream)
    {
        IImageDecoder decoder = DiscoverDecoder(options, stream);
        return decoder.Identify(options, stream);
    }

    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="ImageInfo"/>.</returns>
    private static async Task<ImageInfo> InternalIdentifyAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken)
    {
        IImageDecoder decoder = await DiscoverDecoderAsync(options, stream, cancellationToken);
        return await decoder.IdentifyAsync(options, stream, cancellationToken);
    }
}
