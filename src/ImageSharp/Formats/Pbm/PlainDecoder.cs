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
    public static void Process<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        PbmColorType colorType,
        PbmComponentType componentType,
        int maxPixelValue)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Full-range inputs keep the existing byte and ushort conversion paths.
        // Other ranges need normalized floats until the destination pixel format performs its final packing.
        int fullRange = componentType == PbmComponentType.Short ? ushort.MaxValue : byte.MaxValue;
        if (colorType != PbmColorType.BlackAndWhite && maxPixelValue != fullRange)
        {
            ProcessScaled(configuration, pixels, stream, colorType, maxPixelValue);
            return;
        }

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
    /// Normalizes decimal samples before the destination pixel format packs them.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The destination pixels.</param>
    /// <param name="stream">The input stream at the raster.</param>
    /// <param name="colorType">The encoded color type.</param>
    /// <param name="maxPixelValue">The declared maximum sample value.</param>
    private static void ProcessScaled<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        PbmColorType colorType,
        int maxPixelValue)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Header parsing can reach EOF before the first sample. Retain the destination's default pixels in that case.
        if (stream.Position == stream.Length)
        {
            return;
        }

        bool isRgb = colorType == PbmColorType.Rgb;
        int width = pixels.Width;
        using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(width);
        Span<Vector4> vectorRow = vectors.GetSpan();

        for (int y = 0; y < pixels.Height; y++)
        {
            int count = 0;
            bool hasNext = true;
            for (int x = 0; x < width; x++)
            {
                // ReadDecimal retains a final number even when EOF immediately follows its last digit.
                hasNext = stream.ReadDecimal(out int red) && stream.SkipWhitespaceAndComments();
                int green = red;
                int blue = red;
                if (isRgb)
                {
                    // Preserve available channels if the last pixel is incomplete. Missing channels remain zero.
                    green = 0;
                    blue = 0;
                    if (hasNext)
                    {
                        hasNext = stream.ReadDecimal(out green) && stream.SkipWhitespaceAndComments();
                        if (hasNext)
                        {
                            hasNext = stream.ReadDecimal(out blue) && stream.SkipWhitespaceAndComments();
                        }
                    }
                }

                vectorRow[x] = PbmSampleScaler.Scale(new Vector4(red, green, blue, maxPixelValue), maxPixelValue);
                count++;
                if (!hasNext)
                {
                    break;
                }
            }

            // Do not copy the unused part of a pooled row after EOF. Earlier rows and available samples remain decoded.
            PixelOperations<TPixel>.Instance.FromVector4Destructive(
                configuration,
                vectorRow[..count],
                pixels.DangerousGetRowSpan(y)[..count],
                PixelConversionModifiers.Scale);

            if (!hasNext)
            {
                return;
            }
        }
    }

    private static void ProcessGrayscale<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                stream.ReadDecimal(out int value);
                rowSpan[x] = new L8((byte)Math.Clamp(value, 0, byte.MaxValue));
                count++;
                eofReached = !stream.SkipWhitespaceAndComments();
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

            if (eofReached)
            {
                return;
            }
        }
    }

    private static void ProcessWideGrayscale<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L16> row = allocator.Allocate<L16>(width);
        Span<L16> rowSpan = row.GetSpan();

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                stream.ReadDecimal(out int value);
                rowSpan[x] = new L16((ushort)Math.Clamp(value, 0, ushort.MaxValue));
                count++;
                eofReached = !stream.SkipWhitespaceAndComments();
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

            if (eofReached)
            {
                return;
            }
        }
    }

    private static void ProcessRgb<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<Rgb24> row = allocator.Allocate<Rgb24>(width);
        Span<Rgb24> rowSpan = row.GetSpan();

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                if (!stream.ReadDecimal(out int red) ||
                    !stream.SkipWhitespaceAndComments() ||
                    !stream.ReadDecimal(out int green) ||
                    !stream.SkipWhitespaceAndComments())
                {
                    // Reached EOF before reading a full RGB value
                    eofReached = true;
                    break;
                }

                stream.ReadDecimal(out int blue);

                rowSpan[x] = new Rgb24(
                    (byte)Math.Clamp(red, 0, byte.MaxValue),
                    (byte)Math.Clamp(green, 0, byte.MaxValue),
                    (byte)Math.Clamp(blue, 0, byte.MaxValue));

                count++;
                eofReached = !stream.SkipWhitespaceAndComments();
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

            if (eofReached)
            {
                return;
            }
        }
    }

    private static void ProcessWideRgb<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<Rgb48> row = allocator.Allocate<Rgb48>(width);
        Span<Rgb48> rowSpan = row.GetSpan();

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                if (!stream.ReadDecimal(out int red) ||
                    !stream.SkipWhitespaceAndComments() ||
                    !stream.ReadDecimal(out int green) ||
                    !stream.SkipWhitespaceAndComments())
                {
                    // Reached EOF before reading a full RGB value
                    eofReached = true;
                    break;
                }

                stream.ReadDecimal(out int blue);

                rowSpan[x] = new Rgb48(
                    (ushort)Math.Clamp(red, 0, ushort.MaxValue),
                    (ushort)Math.Clamp(green, 0, ushort.MaxValue),
                    (ushort)Math.Clamp(blue, 0, ushort.MaxValue));

                count++;
                eofReached = !stream.SkipWhitespaceAndComments();
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

            if (eofReached)
            {
                return;
            }
        }
    }

    private static void ProcessBlackAndWhite<TPixel>(Configuration configuration, Buffer2D<TPixel> pixels, BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int height = pixels.Height;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        using IMemoryOwner<L8> row = allocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();

        bool eofReached = false;
        for (int y = 0; y < height; y++)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                stream.ReadDecimal(out int value);

                rowSpan[x] = value == 0 ? White : Black;
                count++;
                eofReached = !stream.SkipWhitespaceAndComments();
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

            if (eofReached)
            {
                return;
            }
        }
    }
}
