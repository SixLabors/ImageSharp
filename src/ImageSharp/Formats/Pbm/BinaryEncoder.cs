// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Pixel encoding methods for the PBM binary encoding.
/// </summary>
internal class BinaryEncoder
{
    /// <summary>
    /// Decode pixels into the PBM binary encoding.
    /// </summary>
    /// <typeparam name="TPixel">The type of input pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="stream">The byte stream to write to.</param>
    /// <param name="image">The input image.</param>
    /// <param name="colorType">The ColorType to use.</param>
    /// <param name="componentType">Data type of the pixels components.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ImageFormatException">
    /// Thrown if an invalid combination of setting is requested.
    /// </exception>
    public static void WritePixels<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        PbmColorType colorType,
        PbmComponentType componentType,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (colorType == PbmColorType.Grayscale)
        {
            if (componentType == PbmComponentType.Byte)
            {
                WriteGrayscale(configuration, stream, image, cancellationToken);
            }
            else if (componentType == PbmComponentType.Short)
            {
                WriteWideGrayscale(configuration, stream, image, cancellationToken);
            }
            else
            {
                throw new ImageFormatException("Component type not supported for Grayscale PBM.");
            }
        }
        else if (colorType == PbmColorType.Rgb)
        {
            if (componentType == PbmComponentType.Byte)
            {
                WriteRgb(configuration, stream, image, cancellationToken);
            }
            else if (componentType == PbmComponentType.Short)
            {
                WriteWideRgb(configuration, stream, image, cancellationToken);
            }
            else
            {
                throw new ImageFormatException("Component type not supported for Color PBM.");
            }
        }
        else if (componentType == PbmComponentType.Bit)
        {
            WriteBlackAndWhite(configuration, stream, image, cancellationToken);
        }
    }

    private static void WriteGrayscale<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);

            PixelOperations<TPixel>.Instance.ToL8Bytes(
                configuration,
                pixelSpan,
                rowSpan,
                width);

            stream.Write(rowSpan);
        }
    }

    private static void WriteWideGrayscale<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int bytesPerPixel = 2;
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);

            PixelOperations<TPixel>.Instance.ToL16Bytes(
                configuration,
                pixelSpan,
                rowSpan,
                width);

            stream.Write(rowSpan);
        }
    }

    private static void WriteRgb<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int bytesPerPixel = 3;
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);

            PixelOperations<TPixel>.Instance.ToRgb24Bytes(
                configuration,
                pixelSpan,
                rowSpan,
                width);

            stream.Write(rowSpan);
        }
    }

    private static void WriteWideRgb<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int bytesPerPixel = 6;
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);

            PixelOperations<TPixel>.Instance.ToRgb48Bytes(
                configuration,
                pixelSpan,
                rowSpan,
                width);

            stream.Write(rowSpan);
        }
    }

    private static void WriteBlackAndWhite<TPixel>(
        Configuration
        configuration,
        Stream stream,
        ImageFrame<TPixel> image,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        Buffer2D<TPixel> pixelBuffer = image.PixelBuffer;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Span<TPixel> pixelSpan = pixelBuffer.DangerousGetRowSpan(y);

            PixelOperations<TPixel>.Instance.ToL8(
                configuration,
                pixelSpan,
                rowSpan);

            for (int x = 0; x < width;)
            {
                int value = 0;
                int stopBit = Math.Min(8, width - x);
                for (int i = 0; i < stopBit; i++)
                {
                    if (rowSpan[x].PackedValue < 128)
                    {
                        value |= 0x80 >> i;
                    }

                    x++;
                }

                stream.WriteByte((byte)value);
            }
        }
    }
}
