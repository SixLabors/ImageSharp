// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
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
            for (int x = 0; x < width; x++)
            {
                stream.ReadDecimal(out int value);
                rowSpan[x] = new L8((byte)value);
                eofReached = !stream.SkipWhitespaceAndComments();
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL8(
                configuration,
                rowSpan,
                pixelSpan);

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
            for (int x = 0; x < width; x++)
            {
                stream.ReadDecimal(out int value);
                rowSpan[x] = new L16((ushort)value);
                eofReached = !stream.SkipWhitespaceAndComments();
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL16(
                configuration,
                rowSpan,
                pixelSpan);

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

                rowSpan[x] = new Rgb24((byte)red, (byte)green, (byte)blue);
                eofReached = !stream.SkipWhitespaceAndComments();
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgb24(
                configuration,
                rowSpan,
                pixelSpan);

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

                rowSpan[x] = new Rgb48((ushort)red, (ushort)green, (ushort)blue);
                eofReached = !stream.SkipWhitespaceAndComments();
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgb48(
                configuration,
                rowSpan,
                pixelSpan);

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
            for (int x = 0; x < width; x++)
            {
                stream.ReadDecimal(out int value);

                rowSpan[x] = value == 0 ? White : Black;
                eofReached = !stream.SkipWhitespaceAndComments();
                if (eofReached)
                {
                    break;
                }
            }

            Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL8(
                configuration,
                rowSpan,
                pixelSpan);

            if (eofReached)
            {
                return;
            }
        }
    }
}
