// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Pixel decoding methods for the PBM plain encoding.
/// </summary>
internal class PlainDecoder
{
    /// <summary>
    /// Decode the specified pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to encode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel array to encode into.</param>
    /// <param name="stream">The stream to read the data from. The header leaves it at the first byte of the raster.</param>
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
        if (colorType == PbmColorType.BlackAndWhite)
        {
            ProcessBlackAndWhite(configuration, pixels, stream, handling);
            return;
        }

        bool isRgb = colorType == PbmColorType.Rgb;
        bool isWide = componentType == PbmComponentType.Short;
        using IMemoryOwner<Vector4>? vectors = PbmSampleScaler.AllocateScaledRow(configuration.MemoryAllocator, pixels.Width, isWide, maxPixelValue);
        if (vectors is null)
        {
            ProcessFullRange(configuration, pixels, stream, isRgb, isWide, handling);
        }
        else
        {
            ProcessScaled(configuration, pixels, stream, isRgb, maxPixelValue, vectors.GetSpan(), handling);
        }
    }

    /// <summary>
    /// Reads one decimal sample and the whitespace and comments after it. The method is the end-of-data site for the plain samples. The caller reads a sample
    /// only when the image needs it.
    /// </summary>
    /// <param name="stream">The input stream. It is at the first byte of the sample token, or at the end of the stream.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <param name="value">The sample value. The value of a damaged token is not defined.</param>
    /// <param name="isValid"><see langword="true"/> if the token is a decimal number. Otherwise, <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the stream holds the sample. <see langword="false"/> if the stream ends before the sample. In that case, the method reported
    /// the truncated raster.
    /// </returns>
    private static bool ReadSample(BufferedReadStream stream, SegmentIntegrityHandling handling, out int value, out bool isValid)
    {
        // ReadDecimal returns false at the end of the stream. If the stream ends directly after the last digit of a number, ReadDecimal keeps the number, and
        // the token is valid. Without a digit, the stream ends before the sample.
        if (!stream.ReadDecimal(out value, out _, out isValid))
        {
            if (!isValid)
            {
                // In the IgnoreImageData mode, the decoded rows and samples stay in the image.
                handling.ThrowOrIgnoreImageDataError(PbmConstants.TruncatedRasterMessage);
                return false;
            }

            return true;
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
            }
            while (current >= 0 && current is not (0x09 or 0x0a or 0x0d or 0x20));
        }

        // At the end of the stream, the next ReadSample call reports the truncation if the image needs more samples.
        stream.SkipWhitespaceAndComments();
        return true;
    }

    /// <summary>
    /// Reads one plain PBM pixel and the whitespace and comments after it. The method is the end-of-data site for the plain bitmap. The caller reads a pixel
    /// only when the image needs it.
    /// </summary>
    /// <param name="stream">The input stream. It is at the byte of the pixel, or at the end of the stream.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <param name="isBlack"><see langword="true"/> if the byte is '1'. Otherwise, <see langword="false"/>.</param>
    /// <param name="isValid"><see langword="true"/> if the byte is '0' or '1'. Otherwise, <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the stream holds the pixel. <see langword="false"/> if the stream ends before the pixel. In that case, the method reported the
    /// truncated raster.
    /// </returns>
    private static bool ReadBit(BufferedReadStream stream, SegmentIntegrityHandling handling, out bool isBlack, out bool isValid)
    {
        // Each pixel is one ASCII '0' or '1' byte, and whitespace in the raster is ignored. Thus "0101" holds four
        // pixels. See Netpbm pbm(5), section "Plain PBM". Any other byte is one damaged pixel.
        int value = stream.ReadByte();
        isBlack = value == '1';
        isValid = isBlack || value == '0';
        if (value < 0)
        {
            // In the IgnoreImageData mode, the decoded rows and pixels stay in the image.
            handling.ThrowOrIgnoreImageDataError(PbmConstants.TruncatedRasterMessage);
            return false;
        }

        if (!isValid)
        {
            handling.ThrowOrIgnoreImageDataError("PBM sample data contains a byte that is not 0 or 1.");
        }

        // At the end of the stream, the next ReadBit call reports the truncation if the image needs more pixels.
        stream.SkipWhitespaceAndComments();
        return true;
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
    /// Decodes plain gray (PGM) and color (PPM) samples with a non-standard Maxval. A gray pixel is one decimal sample. A color pixel is three decimal samples
    /// in red, green, blue order. Each pixel goes to the vector row in one step. If the stream ends early, the decoded samples stay in the image.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="isRgb">True for red, green and blue samples. False for gray samples.</param>
    /// <param name="maxPixelValue">The declared maximum sample value.</param>
    /// <param name="vectorRow">The vector row from <see cref="PbmSampleScaler.AllocateScaledRow"/>.</param>
    /// <param name="handling">The segment integrity mode.</param>
    private static void ProcessScaled<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        bool isRgb,
        int maxPixelValue,
        Span<Vector4> vectorRow,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = pixels.Width;
        int channelCount = isRgb ? 3 : 1;
        List<int> damagedPixels = [];

        for (int y = 0; y < pixels.Height; y++)
        {
            int count = 0;
            int samplesRead = channelCount;
            while (count < width && samplesRead == channelCount)
            {
                samplesRead = ReadPixel(stream, handling, isRgb, out int red, out int green, out int blue, out bool isValid);
                if (samplesRead == 0)
                {
                    break;
                }

                if (!isValid)
                {
                    damagedPixels.Add(count);
                }

                vectorRow[count] = PbmSampleScaler.Scale(red, green, blue, maxPixelValue);
                count++;
            }

            Span<TPixel> destination = pixels.DangerousGetRowSpan(y)[..count];
            PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(configuration, vectorRow[..count], destination);
            ClearDamagedPixels(destination, damagedPixels);

            // ReadSample reported the end of the data. The rows after it keep the fill value.
            if (samplesRead < channelCount)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Reads the samples of one plain pixel: one gray sample, or red, green and blue samples.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <param name="isRgb">True for red, green and blue samples. False for gray samples.</param>
    /// <param name="red">The red sample, or the gray sample.</param>
    /// <param name="green">The green sample. For gray, the gray sample.</param>
    /// <param name="blue">The blue sample. For gray, the gray sample.</param>
    /// <param name="isValid"><see langword="true"/> if each sample that the method read is a decimal number.</param>
    /// <returns>
    /// The number of samples that the method read. It is less than the sample count of a pixel only when the data ends. The pixel keeps its available channels,
    /// and its missing channels are zero.
    /// </returns>
    private static int ReadPixel(
        BufferedReadStream stream,
        SegmentIntegrityHandling handling,
        bool isRgb,
        out int red,
        out int green,
        out int blue,
        out bool isValid)
    {
        green = 0;
        blue = 0;
        if (!ReadSample(stream, handling, out red, out isValid))
        {
            return 0;
        }

        if (!isRgb)
        {
            green = red;
            blue = red;
            return 1;
        }

        if (!ReadSample(stream, handling, out int value, out bool isSampleValid))
        {
            return 1;
        }

        green = value;
        isValid &= isSampleValid;
        if (!ReadSample(stream, handling, out value, out isSampleValid))
        {
            return 2;
        }

        blue = value;
        isValid &= isSampleValid;
        return 3;
    }

    /// <summary>
    /// Decodes full-range plain gray (PGM) and color (PPM) samples: Maxval 255, or Maxval 65535. A gray pixel is one decimal sample. A color pixel is three
    /// decimal samples in red, green, blue order. If the stream ends early, the decoded samples stay in the image.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel to decode to.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pixels">The pixel buffer to decode into.</param>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="isRgb">True for red, green and blue samples. False for gray samples.</param>
    /// <param name="isWide">True for 16-bit samples. False for 8-bit samples.</param>
    /// <param name="handling">The segment integrity mode.</param>
    private static void ProcessFullRange<TPixel>(
        Configuration configuration,
        Buffer2D<TPixel> pixels,
        BufferedReadStream stream,
        bool isRgb,
        bool isWide,
        SegmentIntegrityHandling handling)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int channelCount = isRgb ? 3 : 1;
        int sampleSize = isWide ? sizeof(ushort) : sizeof(byte);
        int fullRange = isWide ? ushort.MaxValue : byte.MaxValue;
        int width = pixels.Width;
        int rowSampleCount = width * channelCount;

        // The row holds the samples in the binary raster layout, in native byte order. Thus the plain and the binary decoders share the row conversion.
        using IMemoryOwner<byte> row = configuration.MemoryAllocator.Allocate<byte>(rowSampleCount * sampleSize);
        Span<byte> rowSpan = row.GetSpan();
        Span<ushort> wideRow = MemoryMarshal.Cast<byte, ushort>(rowSpan);
        List<int> damagedPixels = [];

        for (int y = 0; y < pixels.Height; y++)
        {
            int sampleCount = 0;
            for (; sampleCount < rowSampleCount; sampleCount++)
            {
                if (!ReadSample(stream, handling, out int value, out bool isValid))
                {
                    break;
                }

                if (!isValid)
                {
                    damagedPixels.Add(sampleCount / channelCount);
                }

                // The method saturates a value that is more than the sample range.
                value = Math.Min(value, fullRange);
                if (isWide)
                {
                    wideRow[sampleCount] = (ushort)value;
                }
                else
                {
                    rowSpan[sampleCount] = (byte)value;
                }
            }

            // An incomplete last pixel keeps its available channels. Its missing channels are zero.
            int count = (sampleCount + channelCount - 1) / channelCount;
            int rowLength = count * channelCount * sampleSize;
            rowSpan[(sampleCount * sampleSize)..rowLength].Clear();

            Span<TPixel> destination = pixels.DangerousGetRowSpan(y)[..count];
            PbmSampleScaler.ToPixels(configuration, rowSpan[..rowLength], destination, isRgb, isWide);
            ClearDamagedPixels(destination, damagedPixels);

            // ReadSample reported the end of the data. The rows after it keep the fill value.
            if (sampleCount < rowSampleCount)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Decodes plain black and white (PBM) pixel data.
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
        using IMemoryOwner<L8> row = configuration.MemoryAllocator.Allocate<L8>(width);
        Span<L8> rowSpan = row.GetSpan();
        List<int> damagedPixels = [];

        for (int y = 0; y < pixels.Height; y++)
        {
            int count = 0;
            for (; count < width; count++)
            {
                if (!ReadBit(stream, handling, out bool isBlack, out bool isValid))
                {
                    break;
                }

                if (!isValid)
                {
                    damagedPixels.Add(count);
                }

                rowSpan[count] = isBlack ? BinaryDecoder.Black : BinaryDecoder.White;
            }

            Span<TPixel> destination = pixels.DangerousGetRowSpan(y)[..count];
            PixelOperations<TPixel>.Instance.FromL8(configuration, rowSpan[..count], destination);
            ClearDamagedPixels(destination, damagedPixels);

            // ReadBit reported the end of the data. The rows after it keep the fill value.
            if (count < width)
            {
                return;
            }
        }
    }
}
