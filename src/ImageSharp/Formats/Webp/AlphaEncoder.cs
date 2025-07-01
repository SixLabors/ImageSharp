// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Methods for encoding the alpha data of a VP8 image.
/// </summary>
internal static class AlphaEncoder
{
    /// <summary>
    /// Encodes the alpha channel data.
    /// Data is either compressed as lossless webp image or uncompressed.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="memoryAllocator">The memory manager.</param>
    /// <param name="skipMetadata">Whether to skip metadata encoding.</param>
    /// <param name="compress">Indicates, if the data should be compressed with the lossless webp compression.</param>
    /// <param name="size">The size in bytes of the alpha data.</param>
    /// <returns>The encoded alpha data.</returns>
    public static IMemoryOwner<byte> EncodeAlpha<TPixel>(
        Buffer2DRegion<TPixel> frame,
        Configuration configuration,
        MemoryAllocator memoryAllocator,
        bool skipMetadata,
        bool compress,
        out int size)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IMemoryOwner<byte> alphaData = ExtractAlphaChannel(frame, configuration, memoryAllocator);

        if (compress)
        {
            const WebpEncodingMethod effort = WebpEncodingMethod.Default;
            const int quality = 8 * (int)effort;
            using Vp8LEncoder lossLessEncoder = new(
                memoryAllocator,
                configuration,
                frame.Width,
                frame.Height,
                quality,
                skipMetadata,
                effort,
                TransparentColorMode.Preserve,
                false,
                0);

            // The transparency information will be stored in the green channel of the ARGB quadruplet.
            // The green channel is allowed extra transformation steps in the specification -- unlike the other channels,
            // that can improve compression.
            using ImageFrame<Bgra32> alphaAsFrame = DispatchAlphaToGreen(configuration, frame, alphaData.GetSpan());

            size = lossLessEncoder.EncodeAlphaImageData(alphaAsFrame.PixelBuffer.GetRegion(), alphaData);

            return alphaData;
        }

        size = frame.Width * frame.Height;
        return alphaData;
    }

    /// <summary>
    /// Store the transparency in the green channel.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="frame">The pixel buffer to encode from.</param>
    /// <param name="alphaData">A byte sequence of length width * height, containing all the 8-bit transparency values in scan order.</param>
    /// <returns>The transparency frame.</returns>
    private static ImageFrame<Bgra32> DispatchAlphaToGreen<TPixel>(Configuration configuration, Buffer2DRegion<TPixel> frame, Span<byte> alphaData)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frame.Width;
        int height = frame.Height;
        ImageFrame<Bgra32> alphaAsFrame = new(configuration, width, height);

        for (int y = 0; y < height; y++)
        {
            Memory<Bgra32> rowBuffer = alphaAsFrame.DangerousGetPixelRowMemory(y);
            Span<Bgra32> pixelRow = rowBuffer.Span;
            Span<byte> alphaRow = alphaData.Slice(y * width, width);

            // TODO: This can be probably simd optimized.
            for (int x = 0; x < width; x++)
            {
                // Leave A/R/B channels zero'd.
                pixelRow[x] = new Bgra32(0, alphaRow[x], 0, 0);
            }
        }

        return alphaAsFrame;
    }

    /// <summary>
    /// Extract the alpha data of the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="memoryAllocator">The memory manager.</param>
    /// <returns>A byte sequence of length width * height, containing all the 8-bit transparency values in scan order.</returns>
    private static IMemoryOwner<byte> ExtractAlphaChannel<TPixel>(Buffer2DRegion<TPixel> frame, Configuration configuration, MemoryAllocator memoryAllocator)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frame.Width;
        int height = frame.Height;

        IMemoryOwner<byte> alphaDataBuffer = memoryAllocator.Allocate<byte>(width * height);
        Span<byte> alphaData = alphaDataBuffer.GetSpan();

        using IMemoryOwner<Rgba32> rowBuffer = memoryAllocator.Allocate<Rgba32>(width);
        Span<Rgba32> rgbaRow = rowBuffer.GetSpan();

        for (int y = 0; y < height; y++)
        {
            Span<TPixel> rowSpan = frame.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToRgba32(configuration, rowSpan, rgbaRow);
            int offset = y * width;
            for (int x = 0; x < width; x++)
            {
                alphaData[offset + x] = rgbaRow[x].A;
            }
        }

        return alphaDataBuffer;
    }
}
