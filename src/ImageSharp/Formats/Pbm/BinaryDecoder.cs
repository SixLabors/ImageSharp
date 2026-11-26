// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
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
    public static readonly L8 White = new(255);

    /// <summary>
    /// The luminance value written for a set bit in the black and white format.
    /// </summary>
    public static readonly L8 Black = new(0);

    /// <summary>
    /// Decode the specified pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="colorType">The color type of the encoded pixels.</param>
    /// <param name="componentType">The data type of the pixel components.</param>
    /// <param name="maxPixelValue">The maximum sample value declared in the header. Unused for black and white.</param>
    /// <param name="handling">The segment integrity mode. It controls if truncated sample data throws.</param>
    public static void Process<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        PbmColorType colorType,
        PbmComponentType componentType,
        int maxPixelValue,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (colorType == PbmColorType.BlackAndWhite)
        {
            ProcessBlackAndWhite(configuration, pixels, stream, handling);
        }
        else
        {
            ProcessSamples(configuration, pixels, stream, colorType, componentType, maxPixelValue, handling);
        }
    }

    /// <summary>
    /// Decodes gray (PGM) and color (PPM) binary samples. A gray pixel is one sample. A color pixel is three samples in red, green, blue order. A sample is one
    /// byte when Maxval is less than 256. Otherwise, it is two bytes, most significant byte first. If the stream ends early, the pixels that the method did not
    /// read keep the fill value.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="colorType">The encoded color type.</param>
    /// <param name="componentType">The encoded sample size.</param>
    /// <param name="maxPixelValue">The declared maximum sample value.</param>
    /// <param name="handling">The segment integrity mode.</param>
    private static void ProcessSamples<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        PbmColorType colorType,
        PbmComponentType componentType,
        int maxPixelValue,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool isRgb = colorType == PbmColorType.Rgb;
        bool isWide = componentType == PbmComponentType.Short;
        int sampleSize = isWide ? sizeof(ushort) : sizeof(byte);
        int bytesPerPixel = sampleSize * (isRgb ? 3 : 1);
        int width = pixels.Width;

        // The method rents the rows one time. The encoded bytes stay separate from the float row, so the float conversion cannot overwrite unread samples.
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> row = allocator.Allocate<byte>(width * bytesPerPixel);
        using IMemoryOwner<Vector4>? vectors = PbmSampleScaler.AllocateScaledRow(allocator, width, isWide, maxPixelValue);
        Span<byte> rowSpan = row.GetSpan();
        Span<Vector4> vectorRow = vectors is null ? default : vectors.GetSpan();

        for (int y = 0; y < pixels.Height; y++)
        {
            int count = ReadRow(stream, rowSpan, bytesPerPixel, handling);
            Span<byte> samples = rowSpan[..(count * bytesPerPixel)];

            // The method converts only the available pixels. A short last row must not copy unused pooled memory into the image.
            Span<TPixel> destination = pixels.DangerousGetRowSpan(y)[..count];
            if (vectorRow.IsEmpty)
            {
                // The binary format stores 16-bit samples most significant byte first, but L16 and Rgb48 use native (little-endian) byte order.
                if (isWide)
                {
                    SwapSampleBytes(samples);
                }

                PbmSampleScaler.ToPixels(configuration, samples, destination, isRgb, isWide);
            }
            else
            {
                for (int x = 0; x < count; x++)
                {
                    int offset = x * bytesPerPixel;
                    int red = isWide ? BinaryPrimitives.ReadUInt16BigEndian(samples.Slice(offset, sampleSize)) : samples[offset];
                    int green = red;
                    int blue = red;
                    if (isRgb)
                    {
                        offset += sampleSize;
                        green = isWide ? BinaryPrimitives.ReadUInt16BigEndian(samples.Slice(offset, sampleSize)) : samples[offset];
                        offset += sampleSize;
                        blue = isWide ? BinaryPrimitives.ReadUInt16BigEndian(samples.Slice(offset, sampleSize)) : samples[offset];
                    }

                    vectorRow[x] = PbmSampleScaler.Scale(red, green, blue, maxPixelValue);
                }

                PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(configuration, vectorRow[..count], destination);
            }

            // ReadRow reported the short row. The rows after it keep the fill value.
            if (count < width)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Reads one encoded row. The method sets only the missing bytes of its last incomplete pixel to zero. A row that ends early is truncated image data. The
    /// method reports it, and the caller converts the available pixels and stops. Thus the method reports the truncation one time.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="row">The reusable encoded row.</param>
    /// <param name="bytesPerPixel">The encoded size of one pixel.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns>The number of available pixels, including an incomplete final pixel.</returns>
    private static int ReadRow(BufferedReadStream stream, Span<byte> row, int bytesPerPixel, SegmentIntegrityHandling handling)
    {
        // BufferedReadStream returns fewer bytes than the row length only at the end of the stream.
        int bytesRead = stream.Read(row);
        if (bytesRead < row.Length)
        {
            handling.ThrowOrIgnoreImageDataError(PbmConstants.TruncatedRasterMessage);
        }

        int count = bytesRead / bytesPerPixel;
        if (bytesRead % bytesPerPixel != 0)
        {
            // Keep the available channels and high sample bytes. Missing bytes must not come from pooled storage.
            count++;
            row.Slice(bytesRead, (count * bytesPerPixel) - bytesRead).Clear();
        }

        return count;
    }

    /// <summary>
    /// Reverses the byte order of each 16-bit sample in the given row when the host is little-endian.
    /// The binary PGM and PPM formats store multi-byte samples most significant byte first. Thus the binary decoder and encoder convert between that order and
    /// the native order.
    /// </summary>
    /// <param name="rowSpan">The row of 16-bit sample data to convert in place.</param>
    public static void SwapSampleBytes(Span<byte> rowSpan)
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
    /// <param name="handling">The segment integrity mode.</param>
    private static void ProcessBlackAndWhite<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int packedLength = (width + 7) >> 3;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<byte> packed = allocator.Allocate<byte>(packedLength);
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<byte> packedSpan = packed.GetSpan();
        Span<L8> rowSpan = row.GetSpan();

        for (int y = 0; y < pixels.Height; y++)
        {
            // ReadRow reads the packed row with one byte for each ReadRow pixel. A short row keeps the pixels of the bytes that the stream holds.
            int byteCount = ReadRow(stream, packedSpan, 1, handling);
            int count = Math.Min(width, byteCount << 3);
            for (int x = 0; x < count; x++)
            {
                // Pixel x is bit (7 - (x % 8)) of byte (x / 8), so the mask is 0x80 shifted right by (x % 8).
                rowSpan[x] = (packedSpan[x >> 3] & (0x80 >> (x & 7))) != 0 ? Black : White;
            }

            PixelOperations<TPixel>.Instance.FromL8(configuration, rowSpan[..count], pixels.DangerousGetRowSpan(y)[..count]);

            // ReadRow reported the short row. The rows after it keep the fill value.
            if (byteCount < packedLength)
            {
                return;
            }
        }
    }
}
