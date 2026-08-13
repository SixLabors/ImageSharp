// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Pixel encoding methods for the PBM binary encoding.
/// </summary>
internal class BinaryEncoder
{
    /// <summary>
    /// Encode pixels into the PBM binary encoding.
    /// </summary>
    /// <typeparam name="TPixel">The type of input pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="stream">The byte stream to write to.</param>
    /// <param name="image">The input image.</param>
    /// <param name="colorType">The color type to use.</param>
    /// <param name="componentType">The data type of the pixel components.</param>
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

    /// <summary>
    /// Encodes 8-bit binary grayscale (PGM) pixel data.
    /// Each pixel is written as a single byte that holds its luminance value.
    /// </summary>
    /// <typeparam name="TPixel">The type of input pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="stream">The byte stream to write to.</param>
    /// <param name="image">The input image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
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

    /// <summary>
    /// Encodes 16-bit binary grayscale (PGM) pixel data.
    /// Each pixel is written as one 16-bit sample, most significant byte first.
    /// </summary>
    /// <typeparam name="TPixel">The type of input pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="stream">The byte stream to write to.</param>
    /// <param name="image">The input image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
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

            // The binary format stores 16-bit samples most significant byte first,
            // but ToL16Bytes produces native (little-endian) byte order.
            SwapSampleBytes(rowSpan);

            stream.Write(rowSpan);
        }
    }

    /// <summary>
    /// Encodes 8-bit binary color (PPM) pixel data.
    /// Each pixel is written as three bytes in red, green, blue order.
    /// </summary>
    /// <typeparam name="TPixel">The type of input pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="stream">The byte stream to write to.</param>
    /// <param name="image">The input image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
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

    /// <summary>
    /// Encodes 16-bit binary color (PPM) pixel data.
    /// Each pixel is written as three 16-bit samples in red, green, blue order, most significant byte first.
    /// </summary>
    /// <typeparam name="TPixel">The type of input pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="stream">The byte stream to write to.</param>
    /// <param name="image">The input image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
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

            // The binary format stores 16-bit samples most significant byte first,
            // but ToRgb48Bytes produces native (little-endian) byte order.
            SwapSampleBytes(rowSpan);

            stream.Write(rowSpan);
        }
    }

    /// <summary>
    /// Reverses the byte order of each 16-bit sample in the given row when the host is little-endian.
    /// The binary PGM and PPM formats store multi-byte samples most significant byte first.
    /// </summary>
    /// <param name="rowSpan">The row of native-endian sample data to convert in place.</param>
    private static void SwapSampleBytes(Span<byte> rowSpan)
    {
        if (BitConverter.IsLittleEndian)
        {
            Span<ushort> samples = MemoryMarshal.Cast<byte, ushort>(rowSpan);
            BinaryPrimitives.ReverseEndianness(samples, samples);
        }
    }

    /// <summary>
    /// Encodes binary black and white (PBM) pixel data.
    /// Each byte holds eight pixels, most significant bit first, and a set bit means black.
    /// A pixel with a luminance value less than 128 is written as black.
    /// Each row starts on a byte boundary, so the last byte of a row can hold unused bits.
    /// </summary>
    /// <typeparam name="TPixel">The type of input pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="stream">The byte stream to write to.</param>
    /// <param name="image">The input image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
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
