// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression;

/// <summary>
/// Methods for undoing the horizontal prediction used in combination with deflate and LZW compressed TIFF images.
/// </summary>
internal static class HorizontalPredictor
{
    /// <summary>
    /// Inverts the horizontal predictor.
    /// </summary>
    /// <param name="pixelBytes">Buffer with decompressed pixel data.</param>
    /// <param name="width">The width of the image or strip.</param>
    /// <param name="colorType">The color type of the pixel data.</param>
    /// <param name="isBigEndian">If set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public static void Undo(Span<byte> pixelBytes, int width, TiffColorType colorType, bool isBigEndian)
    {
        switch (colorType)
        {
            case TiffColorType.BlackIsZero8:
            case TiffColorType.WhiteIsZero8:
            case TiffColorType.PaletteColor:
            case TiffColorType.CieLabPlanar:
                UndoGray8Bit(pixelBytes, width);
                break;
            case TiffColorType.BlackIsZero16:
            case TiffColorType.WhiteIsZero16:
            case TiffColorType.CieLab161616Planar:
                UndoGray16Bit(pixelBytes, width, isBigEndian);
                break;
            case TiffColorType.BlackIsZero32:
            case TiffColorType.WhiteIsZero32:
                UndoGray32Bit(pixelBytes, width, isBigEndian);
                break;
            case TiffColorType.Rgb888:
            case TiffColorType.CieLab:
            case TiffColorType.YCbCr:
                UndoRgb24Bit(pixelBytes, width);
                break;
            case TiffColorType.Rgba8888:
            case TiffColorType.Cmyk:
                UndoRgba32Bit(pixelBytes, width);
                break;
            case TiffColorType.Rgb161616:
            case TiffColorType.CieLab161616:
                UndoRgb48Bit(pixelBytes, width, isBigEndian);
                break;
            case TiffColorType.Rgba16161616:
            case TiffColorType.Cmyk16161616:
                // Prediction treats all four words alike. The fourth CMYK word is K, but uses the same addition as RGBA alpha.
                UndoRgba64Bit(pixelBytes, width, isBigEndian);
                break;
            case TiffColorType.Rgb323232:
                UndoRgb96Bit(pixelBytes, width, isBigEndian);
                break;
            case TiffColorType.Rgba32323232:
                UndoRgba128Bit(pixelBytes, width, isBigEndian);
                break;
        }
    }

    /// <summary>
    /// Inverts the horizontal predictor for each tile row.
    /// </summary>
    /// <param name="pixelBytes">Buffer with decompressed pixel data for a tile.</param>
    /// <param name="tileWidth">Tile width in pixels.</param>
    /// <param name="tileHeight">Tile height in pixels.</param>
    /// <param name="colorType">The color type of the pixel data.</param>
    /// <param name="isBigEndian">If set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public static void UndoTile(Span<byte> pixelBytes, int tileWidth, int tileHeight, TiffColorType colorType, bool isBigEndian)
    {
        for (int y = 0; y < tileHeight; y++)
        {
            UndoRow(pixelBytes, tileWidth, y, colorType, isBigEndian);
        }
    }

    /// <summary>
    /// Inverts the horizontal predictor for one row.
    /// </summary>
    /// <param name="pixelBytes">Buffer with decompressed pixel data.</param>
    /// <param name="width">The width in pixels of the row.</param>
    /// <param name="y">The row index.</param>
    /// <param name="colorType">The color type of the pixel data.</param>
    /// <param name="isBigEndian">If set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public static void UndoRow(Span<byte> pixelBytes, int width, int y, TiffColorType colorType, bool isBigEndian)
    {
        switch (colorType)
        {
            case TiffColorType.BlackIsZero8:
            case TiffColorType.WhiteIsZero8:
            case TiffColorType.PaletteColor:
            case TiffColorType.CieLabPlanar:
                UndoGray8BitRow(pixelBytes, width, y);
                break;

            case TiffColorType.BlackIsZero16:
            case TiffColorType.WhiteIsZero16:
            case TiffColorType.CieLab161616Planar:
                if (isBigEndian)
                {
                    UndoGray16BitBigEndianRow(pixelBytes, width, y);
                }
                else
                {
                    UndoGray16BitLittleEndianRow(pixelBytes, width, y);
                }

                break;

            case TiffColorType.BlackIsZero32:
            case TiffColorType.WhiteIsZero32:
                if (isBigEndian)
                {
                    UndoGray32BitBigEndianRow(pixelBytes, width, y);
                }
                else
                {
                    UndoGray32BitLittleEndianRow(pixelBytes, width, y);
                }

                break;

            case TiffColorType.Rgb888:
            case TiffColorType.CieLab:
            case TiffColorType.YCbCr:
                UndoRgb24BitRow(pixelBytes, width, y);
                break;

            case TiffColorType.Rgba8888:
            case TiffColorType.Cmyk:
                UndoRgba32BitRow(pixelBytes, width, y);
                break;

            case TiffColorType.Rgb161616:
            case TiffColorType.CieLab161616:
                if (isBigEndian)
                {
                    UndoRgb48BitBigEndianRow(pixelBytes, width, y);
                }
                else
                {
                    UndoRgb48BitLittleEndianRow(pixelBytes, width, y);
                }

                break;

            case TiffColorType.Rgba16161616:
            case TiffColorType.Cmyk16161616:
                // Tile rows use the same four-word prediction as strips, including carries within each 16-bit sample.
                if (isBigEndian)
                {
                    UndoRgb64BitBigEndianRow(pixelBytes, width, y);
                }
                else
                {
                    UndoRgb64BitLittleEndianRow(pixelBytes, width, y);
                }

                break;

            case TiffColorType.Rgb323232:
                if (isBigEndian)
                {
                    UndoRgb96BitBigEndianRow(pixelBytes, width, y);
                }
                else
                {
                    UndoRgb96BitLittleEndianRow(pixelBytes, width, y);
                }

                break;

            case TiffColorType.Rgba32323232:
                if (isBigEndian)
                {
                    UndoRgba128BitBigEndianRow(pixelBytes, width, y);
                }
                else
                {
                    UndoRgba128BitLittleEndianRow(pixelBytes, width, y);
                }

                break;
        }
    }

    /// <summary>
    /// Subtracts each preceding pixel's components before compressing integer samples.
    /// </summary>
    /// <param name="rows">Complete rows in the file's native byte order.</param>
    /// <param name="width">The row width in bytes.</param>
    /// <param name="bitsPerPixel">The packed integer pixel depth.</param>
    public static void ApplyHorizontalPrediction(Span<byte> rows, int width, int bitsPerPixel)
    {
        if (bitsPerPixel == 8)
        {
            ApplyHorizontalPrediction<byte>(rows, width, 1);
        }
        else if (bitsPerPixel == 16)
        {
            ApplyHorizontalPrediction<ushort>(rows, width, 1);
        }
        else if (bitsPerPixel is 24 or 32)
        {
            // RGB and CMYK differ in component count, not in the predictor's byte arithmetic.
            ApplyHorizontalPrediction<byte>(rows, width, bitsPerPixel / 8);
        }
        else if (bitsPerPixel == 48 || bitsPerPixel == 64)
        {
            ApplyHorizontalPrediction<ushort>(rows, width, bitsPerPixel / 16);
        }
        else if (bitsPerPixel == 96 || bitsPerPixel == 128)
        {
            ApplyHorizontalPrediction<uint>(rows, width, bitsPerPixel / 32);
        }
    }

    /// <summary>
    /// Differences unsigned integer samples without overwriting the preceding samples before they are read.
    /// </summary>
    /// <typeparam name="T">The unsigned stored sample type: byte, ushort or uint.</typeparam>
    /// <param name="rows">Complete rows of native-endian samples.</param>
    /// <param name="width">The row width in bytes.</param>
    /// <param name="samplesPerPixel">The number of components in each pixel.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void ApplyHorizontalPrediction<T>(Span<byte> rows, int width, int samplesPerPixel)
        where T : unmanaged, IBinaryInteger<T>
    {
        DebugGuard.IsTrue(rows.Length % width == 0, "Values must be equal");
        for (int row = 0; row < rows.Length; row += width)
        {
            Span<T> samples = MemoryMarshal.Cast<byte, T>(rows.Slice(row, width));
            ref T first = ref MemoryMarshal.GetReference(samples);
            int end = samples.Length;

            // Each lane subtracts the same component of the preceding pixel. Walk backward so earlier pixels stay intact.
            // Both loads precede the store because the two vectors overlap. Subtraction wraps at the sample's stored width.
            // The first pixel is unchanged because it has no preceding pixel in this row.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint count = (end - samplesPerPixel).Vector512Count<T>();

                for (nuint i = 0; i < count; i++)
                {
                    end -= Vector512<T>.Count;
                    Vector512<T> current = Vector512.LoadUnsafe(ref first, (uint)end);
                    Vector512<T> previous = Vector512.LoadUnsafe(ref first, (uint)(end - samplesPerPixel));
                    (current - previous).StoreUnsafe(ref first, (uint)end);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint count = (end - samplesPerPixel).Vector256Count<T>();

                for (nuint i = 0; i < count; i++)
                {
                    end -= Vector256<T>.Count;
                    Vector256<T> current = Vector256.LoadUnsafe(ref first, (uint)end);
                    Vector256<T> previous = Vector256.LoadUnsafe(ref first, (uint)(end - samplesPerPixel));
                    (current - previous).StoreUnsafe(ref first, (uint)end);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint count = (end - samplesPerPixel).Vector128Count<T>();

                for (nuint i = 0; i < count; i++)
                {
                    end -= Vector128<T>.Count;
                    Vector128<T> current = Vector128.LoadUnsafe(ref first, (uint)end);
                    Vector128<T> previous = Vector128.LoadUnsafe(ref first, (uint)(end - samplesPerPixel));
                    (current - previous).StoreUnsafe(ref first, (uint)end);
                }
            }

            // Finish the leading remainder in the same backward order, including wraparound for negative differences.
            for (int i = end - 1; i >= samplesPerPixel; i--)
            {
                samples[i] = unchecked(samples[i] - samples[i - samplesPerPixel]);
            }
        }
    }

    private static void UndoGray8BitRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width;
        int height = pixelBytes.Length / rowBytesCount;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        byte pixelValue = rowBytes[0];
        for (int x = 1; x < width; x++)
        {
            pixelValue += rowBytes[x];
            rowBytes[x] = pixelValue;
        }
    }

    private static void UndoGray8Bit(Span<byte> pixelBytes, int width)
    {
        int rowBytesCount = width;
        int height = pixelBytes.Length / rowBytesCount;
        for (int y = 0; y < height; y++)
        {
            UndoGray8BitRow(pixelBytes, width, y);
        }
    }

    private static void UndoGray16BitBigEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 2;
        int height = pixelBytes.Length / rowBytesCount;
        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        ushort pixelValue = TiffUtilities.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
        offset += 2;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 2);
            ushort diff = TiffUtilities.ConvertToUShortBigEndian(rowSpan);
            pixelValue += diff;
            BinaryPrimitives.WriteUInt16BigEndian(rowSpan, pixelValue);
            offset += 2;
        }
    }

    private static void UndoGray16BitLittleEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 2;
        int height = pixelBytes.Length / rowBytesCount;
        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        ushort pixelValue = TiffUtilities.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
        offset += 2;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 2);
            ushort diff = TiffUtilities.ConvertToUShortLittleEndian(rowSpan);
            pixelValue += diff;
            BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, pixelValue);
            offset += 2;
        }
    }

    private static void UndoGray16Bit(Span<byte> pixelBytes, int width, bool isBigEndian)
    {
        int rowBytesCount = width * 2;
        int height = pixelBytes.Length / rowBytesCount;
        if (isBigEndian)
        {
            for (int y = 0; y < height; y++)
            {
                UndoGray16BitBigEndianRow(pixelBytes, width, y);
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                UndoGray16BitLittleEndianRow(pixelBytes, width, y);
            }
        }
    }

    private static void UndoGray32BitBigEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 4;
        int height = pixelBytes.Length / rowBytesCount;

        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        uint pixelValue = TiffUtilities.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
        offset += 4;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 4);
            uint diff = TiffUtilities.ConvertToUIntBigEndian(rowSpan);
            pixelValue += diff;
            BinaryPrimitives.WriteUInt32BigEndian(rowSpan, pixelValue);
            offset += 4;
        }
    }

    private static void UndoGray32BitLittleEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 4;
        int height = pixelBytes.Length / rowBytesCount;

        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        uint pixelValue = TiffUtilities.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
        offset += 4;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 4);
            uint diff = TiffUtilities.ConvertToUIntLittleEndian(rowSpan);
            pixelValue += diff;
            BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, pixelValue);
            offset += 4;
        }
    }

    private static void UndoGray32Bit(Span<byte> pixelBytes, int width, bool isBigEndian)
    {
        int rowBytesCount = width * 4;
        int height = pixelBytes.Length / rowBytesCount;
        if (isBigEndian)
        {
            for (int y = 0; y < height; y++)
            {
                UndoGray32BitBigEndianRow(pixelBytes, width, y);
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                UndoGray32BitLittleEndianRow(pixelBytes, width, y);
            }
        }
    }

    private static void UndoRgb24BitRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 3;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        Span<Rgb24> rowRgb = MemoryMarshal.Cast<byte, Rgb24>(rowBytes)[..width];
        ref Rgb24 rowRgbBase = ref MemoryMarshal.GetReference(rowRgb);
        byte r = rowRgbBase.R;
        byte g = rowRgbBase.G;
        byte b = rowRgbBase.B;

        for (int x = 1; x < rowRgb.Length; x++)
        {
            ref Rgb24 pixel = ref rowRgb[x];
            r += pixel.R;
            g += pixel.G;
            b += pixel.B;
            pixel = new Rgb24(r, g, b);
        }
    }

    private static void UndoRgb24Bit(Span<byte> pixelBytes, int width)
    {
        int rowBytesCount = width * 3;
        int height = pixelBytes.Length / rowBytesCount;
        for (int y = 0; y < height; y++)
        {
            UndoRgb24BitRow(pixelBytes, width, y);
        }
    }

    private static void UndoRgba32BitRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 4;

        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        Span<Rgba32> rowRgb = MemoryMarshal.Cast<byte, Rgba32>(rowBytes)[..width];
        ref Rgba32 rowRgbBase = ref MemoryMarshal.GetReference(rowRgb);
        byte r = rowRgbBase.R;
        byte g = rowRgbBase.G;
        byte b = rowRgbBase.B;
        byte a = rowRgbBase.A;

        for (int x = 1; x < rowRgb.Length; x++)
        {
            ref Rgba32 pixel = ref rowRgb[x];
            r += pixel.R;
            g += pixel.G;
            b += pixel.B;
            a += pixel.A;
            pixel = new Rgba32(r, g, b, a);
        }
    }

    private static void UndoRgba32Bit(Span<byte> pixelBytes, int width)
    {
        int rowBytesCount = width * 4;
        int height = pixelBytes.Length / rowBytesCount;
        for (int y = 0; y < height; y++)
        {
            UndoRgba32BitRow(pixelBytes, width, y);
        }
    }

    private static void UndoRgb48BitBigEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 6;
        int height = pixelBytes.Length / rowBytesCount;

        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        ushort r = TiffUtilities.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort g = TiffUtilities.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort b = TiffUtilities.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
        offset += 2;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaR = TiffUtilities.ConvertToUShortBigEndian(rowSpan);
            r += deltaR;
            BinaryPrimitives.WriteUInt16BigEndian(rowSpan, r);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaG = TiffUtilities.ConvertToUShortBigEndian(rowSpan);
            g += deltaG;
            BinaryPrimitives.WriteUInt16BigEndian(rowSpan, g);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaB = TiffUtilities.ConvertToUShortBigEndian(rowSpan);
            b += deltaB;
            BinaryPrimitives.WriteUInt16BigEndian(rowSpan, b);
            offset += 2;
        }
    }

    private static void UndoRgb48BitLittleEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 6;
        int height = pixelBytes.Length / rowBytesCount;

        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        ushort r = TiffUtilities.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort g = TiffUtilities.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort b = TiffUtilities.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
        offset += 2;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaR = TiffUtilities.ConvertToUShortLittleEndian(rowSpan);
            r += deltaR;
            BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, r);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaG = TiffUtilities.ConvertToUShortLittleEndian(rowSpan);
            g += deltaG;
            BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, g);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaB = TiffUtilities.ConvertToUShortLittleEndian(rowSpan);
            b += deltaB;
            BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, b);
            offset += 2;
        }
    }

    private static void UndoRgb48Bit(Span<byte> pixelBytes, int width, bool isBigEndian)
    {
        int rowBytesCount = width * 6;
        int height = pixelBytes.Length / rowBytesCount;
        if (isBigEndian)
        {
            for (int y = 0; y < height; y++)
            {
                UndoRgb48BitBigEndianRow(pixelBytes, width, y);
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                UndoRgb48BitLittleEndianRow(pixelBytes, width, y);
            }
        }
    }

    private static void UndoRgb64BitBigEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 8;
        int offset = 0;

        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        ushort r = TiffUtilities.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort g = TiffUtilities.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort b = TiffUtilities.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort a = TiffUtilities.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
        offset += 2;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaR = TiffUtilities.ConvertToUShortBigEndian(rowSpan);
            r += deltaR;
            BinaryPrimitives.WriteUInt16BigEndian(rowSpan, r);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaG = TiffUtilities.ConvertToUShortBigEndian(rowSpan);
            g += deltaG;
            BinaryPrimitives.WriteUInt16BigEndian(rowSpan, g);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaB = TiffUtilities.ConvertToUShortBigEndian(rowSpan);
            b += deltaB;
            BinaryPrimitives.WriteUInt16BigEndian(rowSpan, b);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaA = TiffUtilities.ConvertToUShortBigEndian(rowSpan);
            a += deltaA;
            BinaryPrimitives.WriteUInt16BigEndian(rowSpan, a);
            offset += 2;
        }
    }

    private static void UndoRgb64BitLittleEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 8;
        int offset = 0;

        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        ushort r = TiffUtilities.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort g = TiffUtilities.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort b = TiffUtilities.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
        offset += 2;
        ushort a = TiffUtilities.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
        offset += 2;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaR = TiffUtilities.ConvertToUShortLittleEndian(rowSpan);
            r += deltaR;
            BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, r);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaG = TiffUtilities.ConvertToUShortLittleEndian(rowSpan);
            g += deltaG;
            BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, g);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaB = TiffUtilities.ConvertToUShortLittleEndian(rowSpan);
            b += deltaB;
            BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, b);
            offset += 2;

            rowSpan = rowBytes.Slice(offset, 2);
            ushort deltaA = TiffUtilities.ConvertToUShortLittleEndian(rowSpan);
            a += deltaA;
            BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, a);
            offset += 2;
        }
    }

    private static void UndoRgba64Bit(Span<byte> pixelBytes, int width, bool isBigEndian)
    {
        int rowBytesCount = width * 8;
        int height = pixelBytes.Length / rowBytesCount;
        if (isBigEndian)
        {
            for (int y = 0; y < height; y++)
            {
                UndoRgb64BitBigEndianRow(pixelBytes, width, y);
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                UndoRgb64BitLittleEndianRow(pixelBytes, width, y);
            }
        }
    }

    private static void UndoRgb96BitBigEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 12;

        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        uint r = TiffUtilities.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint g = TiffUtilities.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint b = TiffUtilities.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
        offset += 4;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 4);
            uint deltaR = TiffUtilities.ConvertToUIntBigEndian(rowSpan);
            r += deltaR;
            BinaryPrimitives.WriteUInt32BigEndian(rowSpan, r);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaG = TiffUtilities.ConvertToUIntBigEndian(rowSpan);
            g += deltaG;
            BinaryPrimitives.WriteUInt32BigEndian(rowSpan, g);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaB = TiffUtilities.ConvertToUIntBigEndian(rowSpan);
            b += deltaB;
            BinaryPrimitives.WriteUInt32BigEndian(rowSpan, b);
            offset += 4;
        }
    }

    private static void UndoRgb96BitLittleEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 12;

        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        uint r = TiffUtilities.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint g = TiffUtilities.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint b = TiffUtilities.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
        offset += 4;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 4);
            uint deltaR = TiffUtilities.ConvertToUIntLittleEndian(rowSpan);
            r += deltaR;
            BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, r);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaG = TiffUtilities.ConvertToUIntLittleEndian(rowSpan);
            g += deltaG;
            BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, g);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaB = TiffUtilities.ConvertToUIntLittleEndian(rowSpan);
            b += deltaB;
            BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, b);
            offset += 4;
        }
    }

    private static void UndoRgb96Bit(Span<byte> pixelBytes, int width, bool isBigEndian)
    {
        int rowBytesCount = width * 12;
        int height = pixelBytes.Length / rowBytesCount;
        if (isBigEndian)
        {
            for (int y = 0; y < height; y++)
            {
                UndoRgb96BitBigEndianRow(pixelBytes, width, y);
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                UndoRgb96BitLittleEndianRow(pixelBytes, width, y);
            }
        }
    }

    private static void UndoRgba128BitBigEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 16;

        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        uint r = TiffUtilities.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint g = TiffUtilities.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint b = TiffUtilities.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint a = TiffUtilities.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
        offset += 4;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 4);
            uint deltaR = TiffUtilities.ConvertToUIntBigEndian(rowSpan);
            r += deltaR;
            BinaryPrimitives.WriteUInt32BigEndian(rowSpan, r);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaG = TiffUtilities.ConvertToUIntBigEndian(rowSpan);
            g += deltaG;
            BinaryPrimitives.WriteUInt32BigEndian(rowSpan, g);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaB = TiffUtilities.ConvertToUIntBigEndian(rowSpan);
            b += deltaB;
            BinaryPrimitives.WriteUInt32BigEndian(rowSpan, b);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaA = TiffUtilities.ConvertToUIntBigEndian(rowSpan);
            a += deltaA;
            BinaryPrimitives.WriteUInt32BigEndian(rowSpan, a);
            offset += 4;
        }
    }

    private static void UndoRgba128BitLittleEndianRow(Span<byte> pixelBytes, int width, int y)
    {
        int rowBytesCount = width * 16;

        int offset = 0;
        Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
        uint r = TiffUtilities.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint g = TiffUtilities.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint b = TiffUtilities.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
        offset += 4;
        uint a = TiffUtilities.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
        offset += 4;

        for (int x = 1; x < width; x++)
        {
            Span<byte> rowSpan = rowBytes.Slice(offset, 4);
            uint deltaR = TiffUtilities.ConvertToUIntLittleEndian(rowSpan);
            r += deltaR;
            BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, r);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaG = TiffUtilities.ConvertToUIntLittleEndian(rowSpan);
            g += deltaG;
            BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, g);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaB = TiffUtilities.ConvertToUIntLittleEndian(rowSpan);
            b += deltaB;
            BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, b);
            offset += 4;

            rowSpan = rowBytes.Slice(offset, 4);
            uint deltaA = TiffUtilities.ConvertToUIntLittleEndian(rowSpan);
            a += deltaA;
            BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, a);
            offset += 4;
        }
    }

    private static void UndoRgba128Bit(Span<byte> pixelBytes, int width, bool isBigEndian)
    {
        int rowBytesCount = width * 16;
        int height = pixelBytes.Length / rowBytesCount;
        if (isBigEndian)
        {
            for (int y = 0; y < height; y++)
            {
                UndoRgba128BitBigEndianRow(pixelBytes, width, y);
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                UndoRgba128BitLittleEndianRow(pixelBytes, width, y);
            }
        }
    }
}
