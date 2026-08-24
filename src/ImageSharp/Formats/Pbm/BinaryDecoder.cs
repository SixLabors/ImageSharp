// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Pixel decoding methods for the PBM binary encoding.
/// </summary>
internal class BinaryDecoder
{
    /// <summary>
    /// The luminance value written for an unset bit in the black and white format.
    /// </summary>
    private static L8 white = new(255);

    /// <summary>
    /// The luminance value written for a set bit in the black and white format.
    /// </summary>
    private static L8 black = new(0);

    /// <summary>
    /// Decode the specified pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="colorType">The color type of the encoded pixels.</param>
    /// <param name="componentType">The data type of the pixel components.</param>
    public static void Process<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream, PbmColorType colorType, PbmComponentType componentType)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (colorType == PbmColorType.Grayscale)
        {
            if (componentType == PbmComponentType.Byte)
            {
                ProcessGrayscale(configuration, pixels, stream);
            }
            else
            {
                ProcessWideGrayscale(configuration, pixels, stream);
            }
        }
        else if (colorType == PbmColorType.Rgb)
        {
            if (componentType == PbmComponentType.Byte)
            {
                ProcessRgb(configuration, pixels, stream);
            }
            else
            {
                ProcessWideRgb(configuration, pixels, stream);
            }
        }
        else
        {
            ProcessBlackAndWhite(configuration, pixels, stream);
        }
    }

    /// <summary>
    /// Decodes 8-bit binary grayscale (PGM) pixel data.
    /// Each pixel is a single byte that holds its luminance value.
    /// When the stream ends early, the rows that were not read keep their default value.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    private static void ProcessGrayscale<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int bytesPerPixel = 1;
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (stream.Read(rowSpan) < rowSpan.Length)
            {
                return;
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL8Bytes(
                configuration,
                rowSpan,
                pixelSpan,
                width);
        }
    }

    /// <summary>
    /// Decodes 16-bit binary grayscale (PGM) pixel data.
    /// Each pixel is one 16-bit sample, stored most significant byte first.
    /// When the stream ends early, the rows that were not read keep their default value.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    private static void ProcessWideGrayscale<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int bytesPerPixel = 2;
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (stream.Read(rowSpan) < rowSpan.Length)
            {
                return;
            }

            // The binary format stores 16-bit samples most significant byte first,
            // but L16 expects native (little-endian) byte order.
            SwapSampleBytes(rowSpan);

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL16Bytes(
                configuration,
                rowSpan,
                pixelSpan,
                width);
        }
    }

    /// <summary>
    /// Decodes 8-bit binary color (PPM) pixel data.
    /// Each pixel is three bytes in red, green, blue order.
    /// When the stream ends early, the rows that were not read keep their default value.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    private static void ProcessRgb<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int bytesPerPixel = 3;
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (stream.Read(rowSpan) < rowSpan.Length)
            {
                return;
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgb24Bytes(
                configuration,
                rowSpan,
                pixelSpan,
                width);
        }
    }

    /// <summary>
    /// Decodes 16-bit binary color (PPM) pixel data.
    /// Each pixel is three 16-bit samples in red, green, blue order, stored most significant byte first.
    /// When the stream ends early, the rows that were not read keep their default value.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    private static void ProcessWideRgb<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const int bytesPerPixel = 6;
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            if (stream.Read(rowSpan) < rowSpan.Length)
            {
                return;
            }

            // The binary format stores 16-bit samples most significant byte first,
            // but Rgb48 expects native (little-endian) byte order.
            SwapSampleBytes(rowSpan);

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgb48Bytes(
                configuration,
                rowSpan,
                pixelSpan,
                width);
        }
    }

    /// <summary>
    /// Reverses the byte order of each 16-bit sample in the given row when the host is little-endian.
    /// The binary PGM and PPM formats store multi-byte samples most significant byte first.
    /// </summary>
    /// <param name="rowSpan">The row of big-endian sample data to convert in place.</param>
    private static void SwapSampleBytes(Span<byte> rowSpan)
    {
        if (BitConverter.IsLittleEndian)
        {
            Span<ushort> samples = MemoryMarshal.Cast<byte, ushort>(rowSpan);
            BinaryPrimitives.ReverseEndianness(samples, samples);
        }
    }

    /// <summary>
    /// Decodes binary black and white (PBM) pixel data.
    /// Each byte holds eight pixels, most significant bit first, and a set bit means black.
    /// Each row starts on a byte boundary, so the last byte of a row can hold unused bits.
    /// When the stream ends early, the pixels that were not read keep their default value.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    private static void ProcessBlackAndWhite<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width;)
            {
                int raw = stream.ReadByte();
                if (raw < 0)
                {
                    return;
                }

                int stopBit = Math.Min(8, width - x);
                for (int bit = 0; bit < stopBit; bit++)
                {
                    bool bitValue = (raw & (0x80 >> bit)) != 0;
                    rowSpan[x] = bitValue ? black : white;
                    x++;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL8(
                configuration,
                rowSpan,
                pixelSpan);
        }
    }
}
