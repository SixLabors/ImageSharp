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
        ImageFrame<TPixel> frame,
        Configuration configuration,
        MemoryAllocator memoryAllocator,
        bool skipMetadata,
        bool compress,
        out int size)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frame.Width;
        int height = frame.Height;
        IMemoryOwner<byte> alphaData = ExtractAlphaChannel(frame, configuration, memoryAllocator);

        if (compress)
        {
            const WebpEncodingMethod effort = WebpEncodingMethod.Default;
            const int quality = 8 * (int)effort;
            using Vp8LEncoder lossLessEncoder = new(
                memoryAllocator,
                configuration,
                width,
                height,
                quality,
                skipMetadata,
                effort,
                WebpTransparentColorMode.Preserve,
                false,
                0);

            // The transparency information will be stored in the green channel of the ARGB quadruplet.
            // The green channel is allowed extra transformation steps in the specification -- unlike the other channels,
            // that can improve compression.
            using ImageFrame<Rgba32> alphaAsFrame = DispatchAlphaToGreen(frame, alphaData.GetSpan());

            size = lossLessEncoder.EncodeAlphaImageData(alphaAsFrame, alphaData);

            return alphaData;
        }

        size = width * height;
        return alphaData;
    }

    /// <summary>
    /// Store the transparency in the green channel.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="alphaData">A byte sequence of length width * height, containing all the 8-bit transparency values in scan order.</param>
    /// <returns>The transparency frame.</returns>
    private static ImageFrame<Rgba32> DispatchAlphaToGreen<TPixel>(ImageFrame<TPixel> frame, Span<byte> alphaData)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frame.Width;
        int height = frame.Height;
        ImageFrame<Rgba32> alphaAsFrame = new(Configuration.Default, width, height);

        for (int y = 0; y < height; y++)
        {
            Memory<Rgba32> rowBuffer = alphaAsFrame.DangerousGetPixelRowMemory(y);
            Span<Rgba32> pixelRow = rowBuffer.Span;
            Span<byte> alphaRow = alphaData.Slice(y * width, width);
            for (int x = 0; x < width; x++)
            {
                // Leave A/R/B channels zero'd.
                pixelRow[x] = new(0, alphaRow[x], 0, 0);
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
    private static IMemoryOwner<byte> ExtractAlphaChannel<TPixel>(ImageFrame<TPixel> frame, Configuration configuration, MemoryAllocator memoryAllocator)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2D<TPixel> imageBuffer = frame.PixelBuffer;
        int height = frame.Height;
        int width = frame.Width;
        IMemoryOwner<byte> alphaDataBuffer = memoryAllocator.Allocate<byte>(width * height);
        Span<byte> alphaData = alphaDataBuffer.GetSpan();

        using IMemoryOwner<Rgba32> rowBuffer = memoryAllocator.Allocate<Rgba32>(width);
        Span<Rgba32> rgbaRow = rowBuffer.GetSpan();

        for (int y = 0; y < height; y++)
        {
            Span<TPixel> rowSpan = imageBuffer.DangerousGetRowSpan(y);
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
