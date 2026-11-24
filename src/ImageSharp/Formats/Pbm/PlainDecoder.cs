// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Pixel decoding methods for the PBM plain encoding.
/// </summary>
internal class PlainDecoder
{
    private static readonly L8 White = new(255);
    private static readonly L8 Black = new(0);

    /// <summary>
    /// Decode the specified pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to encode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel array to encode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="colorType">The ColorType to decode.</param>
    /// <param name="componentType">Data type of the pixles components.</param>
    /// <param name="maxPixelValue">The maximum sample value declared in the header. Unused for black and white.</param>
    /// <param name="handling">
    /// The segment integrity mode. It controls if truncated sample data or a damaged sample throws.
    /// </param>
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
        // Header parsing can reach EOF before the first sample. The pixels keep their default value in that case.
        bool complete = false;
        if (stream.Position < stream.Length)
        {
            // Full-range inputs keep the existing byte and ushort conversion paths.
            // Other ranges need normalized floats until the destination pixel format performs its final packing.
            int fullRange = componentType == PbmComponentType.Short ? ushort.MaxValue : byte.MaxValue;
            if (colorType != PbmColorType.BlackAndWhite && maxPixelValue != fullRange)
            {
                complete = ProcessScaled(configuration, pixels, stream, colorType, maxPixelValue, handling);
            }
            else if (colorType == PbmColorType.Grayscale)
            {
                complete = componentType == PbmComponentType.Byte
                    ? ProcessGrayscale(configuration, pixels, stream, handling)
                    : ProcessWideGrayscale(configuration, pixels, stream, handling);
            }
            else if (colorType == PbmColorType.Rgb)
            {
                complete = componentType == PbmComponentType.Byte
                    ? ProcessRgb(configuration, pixels, stream, handling)
                    : ProcessWideRgb(configuration, pixels, stream, handling);
            }
            else
            {
                complete = ProcessBlackAndWhite(configuration, pixels, stream, handling);
            }
        }

        if (!complete)
        {
            // In the IgnoreImageData mode, the decoded rows and samples stay in the image.
            handling.ThrowOrIgnoreImageDataError(PbmConstants.TruncatedRasterMessage);
        }
    }

    /// <summary>
    /// Reads one decimal sample and the whitespace and comments after it.
    /// </summary>
    /// <param name="stream">The input stream. It is at the first byte of the sample token.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <param name="value">The sample value. The value of a damaged token is not defined.</param>
    /// <param name="isValid"><see langword="true"/> if the token is a decimal number; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if more raster data follows the sample; <see langword="false"/> if the stream ends.</returns>
    private static bool ReadSample(BufferedReadStream stream, SegmentIntegrityHandling handling, out int value, out bool isValid)
    {
        // ReadDecimal retains a final number even when EOF immediately follows its last digit.
        if (!stream.ReadDecimal(out value, out _, out isValid))
        {
            return false;
        }

        if (!isValid)
        {
            // A token that is not a decimal number is damaged image data. Whitespace separates the samples, so the
            // damaged token continues to the next whitespace. See Netpbm pgm(5), section "Plain PGM", and ppm(5),
            // section "Plain PPM". In the IgnoreImageData mode, the next token is the next sample.
            handling.ThrowOrIgnoreImageDataError("PBM sample data contains a token that is not a decimal number.");

            // ReadDecimal consumed the first byte of the token that is not a digit.
            int current;
            do
            {
                current = stream.ReadByte();
                if (current < 0)
                {
                    return false;
                }
            }
            while (current is not (0x09 or 0x0a or 0x0d or 0x20));
        }

        return stream.SkipWhitespaceAndComments();
    }

    /// <summary>
    /// Reads one plain PBM pixel and the whitespace and comments after it.
    /// </summary>
    /// <param name="stream">The input stream. It is at the byte of the pixel.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <param name="isBlack"><see langword="true"/> if the byte is '1'; otherwise, <see langword="false"/>.</param>
    /// <param name="isValid"><see langword="true"/> if the byte is '0' or '1'; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if more raster data follows the pixel; <see langword="false"/> if the stream ends.</returns>
    private static bool ReadBit(BufferedReadStream stream, SegmentIntegrityHandling handling, out bool isBlack, out bool isValid)
    {
        // Each pixel is one ASCII '0' or '1' byte, and whitespace in the raster is ignored. Thus "0101" holds four
        // pixels. See Netpbm pbm(5), section "Plain PBM". Any other byte is one damaged pixel.
        int value = stream.ReadByte();
        isBlack = value == '1';
        isValid = isBlack || value == '0';
        if (!isValid)
        {
            handling.ThrowOrIgnoreImageDataError("PBM sample data contains a byte that is not 0 or 1.");
        }

        return stream.SkipWhitespaceAndComments();
    }

    /// <summary>
    /// Sets the damaged pixels of a decoded row to the fill value, the default pixel value.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="pixelRow">The decoded row.</param>
    /// <param name="damagedPixels">The columns of the damaged pixels in the row. The method clears the list.</param>
    private static void ClearDamagedPixels<TPixel>(Span<TPixel> pixelRow, List<int> damagedPixels)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (int x in damagedPixels)
        {
            pixelRow[x] = default;
        }

        damagedPixels.Clear();
    }

    /// <summary>
    /// Normalizes decimal samples before the destination pixel format packs them.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The destination pixels.</param>
    /// <param name="stream">The input stream at the raster.</param>
    /// <param name="colorType">The encoded color type.</param>
    /// <param name="maxPixelValue">The declared maximum sample value.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns><see langword="true"/> if the stream holds each sample of the image; <see langword="false"/> if it ends early.</returns>
    private static bool ProcessScaled<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        PbmColorType colorType,
        int maxPixelValue,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool isRgb = colorType == PbmColorType.Rgb;
        int channelCount = isRgb ? 3 : 1;
        int width = pixels.Width;
        int height = pixels.Height;
        using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(width);
        Span<Vector4> vectorRow = vectors.GetSpan();
        List<int> damagedPixels = [];

        for (int y = 0; y < height; y++)
        {
            int count = 0;
            int channels = 0;
            bool hasNext = true;
            for (int x = 0; x < width; x++)
            {
                channels = 1;
                hasNext = ReadSample(stream, handling, out int red, out bool isValid);
                int green = red;
                int blue = red;
                if (isRgb)
                {
                    // Preserve available channels if the last pixel is incomplete. Missing channels remain zero.
                    green = 0;
                    blue = 0;
                    if (hasNext)
                    {
                        channels++;
                        hasNext = ReadSample(stream, handling, out green, out bool isGreenValid);
                        isValid &= isGreenValid;
                        if (hasNext)
                        {
                            channels++;
                            hasNext = ReadSample(stream, handling, out blue, out bool isBlueValid);
                            isValid &= isBlueValid;
                        }
                    }
                }

                if (!isValid)
                {
                    damagedPixels.Add(x);
                }

                vectorRow[x] = PbmSampleScaler.Scale(new Vector4(red, green, blue, maxPixelValue), maxPixelValue);
                count++;
                if (!hasNext)
                {
                    break;
                }
            }

            // Do not copy the unused part of a pooled row after EOF. Earlier rows and available samples remain decoded.
            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(
                configuration,
                vectorRow[..count],
                pixelSpan[..count]);

            ClearDamagedPixels(pixelSpan, damagedPixels);

            if (!hasNext)
            {
                // The stream can end right after the last channel of the last pixel.
                return y == height - 1 && count == width && channels == channelCount;
            }
        }

        return true;
    }

    /// <summary>
    /// Decodes 8-bit plain grayscale (PGM) pixel data.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns><see langword="true"/> if the stream holds each sample of the image; <see langword="false"/> if it ends early.</returns>
    private static bool ProcessGrayscale<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();
        List<int> damagedPixels = [];

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                eofReached = !ReadSample(stream, handling, out int value, out bool isValid);
                if (!isValid)
                {
                    damagedPixels.Add(x);
                }

                rowSpan[x] = new L8((byte)Math.Clamp(value, 0, byte.MaxValue));
                count++;
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL8(
                configuration,
                rowSpan[..count],
                pixelSpan[..count]);

            ClearDamagedPixels(pixelSpan, damagedPixels);

            if (eofReached)
            {
                // The stream can end right after the last sample of the image.
                return y == height - 1 && count == width;
            }
        }

        return true;
    }

    /// <summary>
    /// Decodes 16-bit plain grayscale (PGM) pixel data.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns><see langword="true"/> if the stream holds each sample of the image; <see langword="false"/> if it ends early.</returns>
    private static bool ProcessWideGrayscale<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L16> row = allocator.Allocate<L16>(width);
        Span<L16> rowSpan = row.GetSpan();
        List<int> damagedPixels = [];

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                eofReached = !ReadSample(stream, handling, out int value, out bool isValid);
                if (!isValid)
                {
                    damagedPixels.Add(x);
                }

                rowSpan[x] = new L16((ushort)Math.Clamp(value, 0, ushort.MaxValue));
                count++;
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL16(
                configuration,
                rowSpan[..count],
                pixelSpan[..count]);

            ClearDamagedPixels(pixelSpan, damagedPixels);

            if (eofReached)
            {
                // The stream can end right after the last sample of the image.
                return y == height - 1 && count == width;
            }
        }

        return true;
    }

    /// <summary>
    /// Decodes 8-bit plain color (PPM) pixel data.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns><see langword="true"/> if the stream holds each sample of the image; <see langword="false"/> if it ends early.</returns>
    private static bool ProcessRgb<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<Rgb24> row = allocator.Allocate<Rgb24>(width);
        Span<Rgb24> rowSpan = row.GetSpan();
        List<int> damagedPixels = [];

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            int channels = 0;
            for (int x = 0; x < width; x++)
            {
                // Preserve available channels if the last pixel is incomplete. Missing channels remain zero.
                int green = 0;
                int blue = 0;
                channels = 1;
                eofReached = !ReadSample(stream, handling, out int red, out bool isValid);
                if (!eofReached)
                {
                    channels++;
                    eofReached = !ReadSample(stream, handling, out green, out bool isGreenValid);
                    isValid &= isGreenValid;
                    if (!eofReached)
                    {
                        channels++;
                        eofReached = !ReadSample(stream, handling, out blue, out bool isBlueValid);
                        isValid &= isBlueValid;
                    }
                }

                if (!isValid)
                {
                    damagedPixels.Add(x);
                }

                rowSpan[x] = new Rgb24(
                    (byte)Math.Clamp(red, 0, byte.MaxValue),
                    (byte)Math.Clamp(green, 0, byte.MaxValue),
                    (byte)Math.Clamp(blue, 0, byte.MaxValue));

                count++;
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgb24(
                configuration,
                rowSpan[..count],
                pixelSpan[..count]);

            ClearDamagedPixels(pixelSpan, damagedPixels);

            if (eofReached)
            {
                // The stream can end right after the last channel of the last pixel.
                return y == height - 1 && count == width && channels == 3;
            }
        }

        return true;
    }

    /// <summary>
    /// Decodes 16-bit plain color (PPM) pixel data.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns><see langword="true"/> if the stream holds each sample of the image; <see langword="false"/> if it ends early.</returns>
    private static bool ProcessWideRgb<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<Rgb48> row = allocator.Allocate<Rgb48>(width);
        Span<Rgb48> rowSpan = row.GetSpan();
        List<int> damagedPixels = [];

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            int channels = 0;
            for (int x = 0; x < width; x++)
            {
                // Preserve available channels if the last pixel is incomplete. Missing channels remain zero.
                int green = 0;
                int blue = 0;
                channels = 1;
                eofReached = !ReadSample(stream, handling, out int red, out bool isValid);
                if (!eofReached)
                {
                    channels++;
                    eofReached = !ReadSample(stream, handling, out green, out bool isGreenValid);
                    isValid &= isGreenValid;
                    if (!eofReached)
                    {
                        channels++;
                        eofReached = !ReadSample(stream, handling, out blue, out bool isBlueValid);
                        isValid &= isBlueValid;
                    }
                }

                if (!isValid)
                {
                    damagedPixels.Add(x);
                }

                rowSpan[x] = new Rgb48(
                    (ushort)Math.Clamp(red, 0, ushort.MaxValue),
                    (ushort)Math.Clamp(green, 0, ushort.MaxValue),
                    (ushort)Math.Clamp(blue, 0, ushort.MaxValue));

                count++;
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgb48(
                configuration,
                rowSpan[..count],
                pixelSpan[..count]);

            ClearDamagedPixels(pixelSpan, damagedPixels);

            if (eofReached)
            {
                // The stream can end right after the last channel of the last pixel.
                return y == height - 1 && count == width && channels == 3;
            }
        }

        return true;
    }

    /// <summary>
    /// Decodes plain black and white (PBM) pixel data.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns><see langword="true"/> if the stream holds each sample of the image; <see langword="false"/> if it ends early.</returns>
    private static bool ProcessBlackAndWhite<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();
        List<int> damagedPixels = [];

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                eofReached = !ReadBit(stream, handling, out bool isBlack, out bool isValid);
                if (!isValid)
                {
                    damagedPixels.Add(x);
                }

                rowSpan[x] = isBlack ? Black : White;
                count++;
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL8(
                configuration,
                rowSpan[..count],
                pixelSpan[..count]);

            ClearDamagedPixels(pixelSpan, damagedPixels);

            if (eofReached)
            {
                // The stream can end right after the last sample of the image.
                return y == height - 1 && count == width;
            }
        }

        return true;
    }
}
