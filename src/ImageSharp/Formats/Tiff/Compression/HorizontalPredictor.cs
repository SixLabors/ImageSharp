// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Methods for undoing the horizontal prediction used in combination with deflate and LZW compressed TIFF images.
    /// </summary>
    internal static class HorizontalPredictor
    {
        /// <summary>
        /// Inverts the horizontal prediction.
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
                    UndoGray8Bit(pixelBytes, width);
                    break;
                case TiffColorType.BlackIsZero16:
                case TiffColorType.WhiteIsZero16:
                    UndoGray16Bit(pixelBytes, width, isBigEndian);
                    break;
                case TiffColorType.BlackIsZero32:
                case TiffColorType.WhiteIsZero32:
                    UndoGray32Bit(pixelBytes, width, isBigEndian);
                    break;
                case TiffColorType.Rgb888:
                case TiffColorType.CieLab:
                    UndoRgb24Bit(pixelBytes, width);
                    break;
                case TiffColorType.Rgba8888:
                    UndoRgba32Bit(pixelBytes, width);
                    break;
                case TiffColorType.Rgb161616:
                    UndoRgb48Bit(pixelBytes, width, isBigEndian);
                    break;
                case TiffColorType.Rgba16161616:
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

        public static void ApplyHorizontalPrediction(Span<byte> rows, int width, int bitsPerPixel)
        {
            if (bitsPerPixel == 8)
            {
                ApplyHorizontalPrediction8Bit(rows, width);
            }
            else if (bitsPerPixel == 24)
            {
                ApplyHorizontalPrediction24Bit(rows, width);
            }
        }

        /// <summary>
        /// Applies a horizontal predictor to the rgb row.
        /// Make use of the fact that many continuous-tone images rarely vary much in pixel value from one pixel to the next.
        /// In such images, if we replace the pixel values by differences between consecutive pixels, many of the differences should be 0, plus
        /// or minus 1, and so on.This reduces the apparent information content and allows LZW to encode the data more compactly.
        /// </summary>
        /// <param name="rows">The rgb pixel rows.</param>
        /// <param name="width">The width.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ApplyHorizontalPrediction24Bit(Span<byte> rows, int width)
        {
            DebugGuard.IsTrue(rows.Length % width == 0, "Values must be equals");
            int height = rows.Length / width;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowSpan = rows.Slice(y * width, width);
                Span<Rgb24> rowRgb = MemoryMarshal.Cast<byte, Rgb24>(rowSpan);

                for (int x = rowRgb.Length - 1; x >= 1; x--)
                {
                    byte r = (byte)(rowRgb[x].R - rowRgb[x - 1].R);
                    byte g = (byte)(rowRgb[x].G - rowRgb[x - 1].G);
                    byte b = (byte)(rowRgb[x].B - rowRgb[x - 1].B);
                    var rgb = new Rgb24(r, g, b);
                    rowRgb[x].FromRgb24(rgb);
                }
            }
        }

        /// <summary>
        /// Applies a horizontal predictor to a gray pixel row.
        /// </summary>
        /// <param name="rows">The gray pixel rows.</param>
        /// <param name="width">The width.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ApplyHorizontalPrediction8Bit(Span<byte> rows, int width)
        {
            DebugGuard.IsTrue(rows.Length % width == 0, "Values must be equals");
            int height = rows.Length / width;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowSpan = rows.Slice(y * width, width);
                for (int x = rowSpan.Length - 1; x >= 1; x--)
                {
                    rowSpan[x] -= rowSpan[x - 1];
                }
            }
        }

        private static void UndoGray8Bit(Span<byte> pixelBytes, int width)
        {
            int rowBytesCount = width;
            int height = pixelBytes.Length / rowBytesCount;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);

                byte pixelValue = rowBytes[0];
                for (int x = 1; x < width; x++)
                {
                    pixelValue += rowBytes[x];
                    rowBytes[x] = pixelValue;
                }
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
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    ushort pixelValue = TiffUtils.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
                    offset += 2;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 2);
                        ushort diff = TiffUtils.ConvertToUShortBigEndian(rowSpan);
                        pixelValue += diff;
                        BinaryPrimitives.WriteUInt16BigEndian(rowSpan, pixelValue);
                        offset += 2;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    ushort pixelValue = TiffUtils.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
                    offset += 2;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 2);
                        ushort diff = TiffUtils.ConvertToUShortLittleEndian(rowSpan);
                        pixelValue += diff;
                        BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, pixelValue);
                        offset += 2;
                    }
                }
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
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    uint pixelValue = TiffUtils.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
                    offset += 4;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 4);
                        uint diff = TiffUtils.ConvertToUIntBigEndian(rowSpan);
                        pixelValue += diff;
                        BinaryPrimitives.WriteUInt32BigEndian(rowSpan, pixelValue);
                        offset += 4;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    uint pixelValue = TiffUtils.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
                    offset += 4;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 4);
                        uint diff = TiffUtils.ConvertToUIntLittleEndian(rowSpan);
                        pixelValue += diff;
                        BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, pixelValue);
                        offset += 4;
                    }
                }
            }
        }

        private static void UndoRgb24Bit(Span<byte> pixelBytes, int width)
        {
            int rowBytesCount = width * 3;
            int height = pixelBytes.Length / rowBytesCount;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                Span<Rgb24> rowRgb = MemoryMarshal.Cast<byte, Rgb24>(rowBytes).Slice(0, width);
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
                    var rgb = new Rgb24(r, g, b);
                    pixel.FromRgb24(rgb);
                }
            }
        }

        private static void UndoRgba32Bit(Span<byte> pixelBytes, int width)
        {
            int rowBytesCount = width * 4;
            int height = pixelBytes.Length / rowBytesCount;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                Span<Rgba32> rowRgb = MemoryMarshal.Cast<byte, Rgba32>(rowBytes).Slice(0, width);
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
                    var rgb = new Rgba32(r, g, b, a);
                    pixel.FromRgba32(rgb);
                }
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
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    ushort r = TiffUtils.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort g = TiffUtils.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort b = TiffUtils.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
                    offset += 2;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaR = TiffUtils.ConvertToUShortBigEndian(rowSpan);
                        r += deltaR;
                        BinaryPrimitives.WriteUInt16BigEndian(rowSpan, r);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaG = TiffUtils.ConvertToUShortBigEndian(rowSpan);
                        g += deltaG;
                        BinaryPrimitives.WriteUInt16BigEndian(rowSpan, g);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaB = TiffUtils.ConvertToUShortBigEndian(rowSpan);
                        b += deltaB;
                        BinaryPrimitives.WriteUInt16BigEndian(rowSpan, b);
                        offset += 2;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    ushort r = TiffUtils.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort g = TiffUtils.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort b = TiffUtils.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
                    offset += 2;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaR = TiffUtils.ConvertToUShortLittleEndian(rowSpan);
                        r += deltaR;
                        BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, r);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaG = TiffUtils.ConvertToUShortLittleEndian(rowSpan);
                        g += deltaG;
                        BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, g);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaB = TiffUtils.ConvertToUShortLittleEndian(rowSpan);
                        b += deltaB;
                        BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, b);
                        offset += 2;
                    }
                }
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
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    ushort r = TiffUtils.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort g = TiffUtils.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort b = TiffUtils.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort a = TiffUtils.ConvertToUShortBigEndian(rowBytes.Slice(offset, 2));
                    offset += 2;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaR = TiffUtils.ConvertToUShortBigEndian(rowSpan);
                        r += deltaR;
                        BinaryPrimitives.WriteUInt16BigEndian(rowSpan, r);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaG = TiffUtils.ConvertToUShortBigEndian(rowSpan);
                        g += deltaG;
                        BinaryPrimitives.WriteUInt16BigEndian(rowSpan, g);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaB = TiffUtils.ConvertToUShortBigEndian(rowSpan);
                        b += deltaB;
                        BinaryPrimitives.WriteUInt16BigEndian(rowSpan, b);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaA = TiffUtils.ConvertToUShortBigEndian(rowSpan);
                        a += deltaA;
                        BinaryPrimitives.WriteUInt16BigEndian(rowSpan, a);
                        offset += 2;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    ushort r = TiffUtils.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort g = TiffUtils.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort b = TiffUtils.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
                    offset += 2;
                    ushort a = TiffUtils.ConvertToUShortLittleEndian(rowBytes.Slice(offset, 2));
                    offset += 2;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaR = TiffUtils.ConvertToUShortLittleEndian(rowSpan);
                        r += deltaR;
                        BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, r);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaG = TiffUtils.ConvertToUShortLittleEndian(rowSpan);
                        g += deltaG;
                        BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, g);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaB = TiffUtils.ConvertToUShortLittleEndian(rowSpan);
                        b += deltaB;
                        BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, b);
                        offset += 2;

                        rowSpan = rowBytes.Slice(offset, 2);
                        ushort deltaA = TiffUtils.ConvertToUShortLittleEndian(rowSpan);
                        a += deltaA;
                        BinaryPrimitives.WriteUInt16LittleEndian(rowSpan, a);
                        offset += 2;
                    }
                }
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
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    uint r = TiffUtils.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint g = TiffUtils.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint b = TiffUtils.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
                    offset += 4;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaR = TiffUtils.ConvertToUIntBigEndian(rowSpan);
                        r += deltaR;
                        BinaryPrimitives.WriteUInt32BigEndian(rowSpan, r);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaG = TiffUtils.ConvertToUIntBigEndian(rowSpan);
                        g += deltaG;
                        BinaryPrimitives.WriteUInt32BigEndian(rowSpan, g);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaB = TiffUtils.ConvertToUIntBigEndian(rowSpan);
                        b += deltaB;
                        BinaryPrimitives.WriteUInt32BigEndian(rowSpan, b);
                        offset += 4;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    uint r = TiffUtils.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint g = TiffUtils.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint b = TiffUtils.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
                    offset += 4;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaR = TiffUtils.ConvertToUIntLittleEndian(rowSpan);
                        r += deltaR;
                        BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, r);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaG = TiffUtils.ConvertToUIntLittleEndian(rowSpan);
                        g += deltaG;
                        BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, g);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaB = TiffUtils.ConvertToUIntLittleEndian(rowSpan);
                        b += deltaB;
                        BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, b);
                        offset += 4;
                    }
                }
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
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    uint r = TiffUtils.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint g = TiffUtils.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint b = TiffUtils.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint a = TiffUtils.ConvertToUIntBigEndian(rowBytes.Slice(offset, 4));
                    offset += 4;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaR = TiffUtils.ConvertToUIntBigEndian(rowSpan);
                        r += deltaR;
                        BinaryPrimitives.WriteUInt32BigEndian(rowSpan, r);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaG = TiffUtils.ConvertToUIntBigEndian(rowSpan);
                        g += deltaG;
                        BinaryPrimitives.WriteUInt32BigEndian(rowSpan, g);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaB = TiffUtils.ConvertToUIntBigEndian(rowSpan);
                        b += deltaB;
                        BinaryPrimitives.WriteUInt32BigEndian(rowSpan, b);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaA = TiffUtils.ConvertToUIntBigEndian(rowSpan);
                        a += deltaA;
                        BinaryPrimitives.WriteUInt32BigEndian(rowSpan, a);
                        offset += 4;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    int offset = 0;
                    Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                    uint r = TiffUtils.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint g = TiffUtils.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint b = TiffUtils.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
                    offset += 4;
                    uint a = TiffUtils.ConvertToUIntLittleEndian(rowBytes.Slice(offset, 4));
                    offset += 4;

                    for (int x = 1; x < width; x++)
                    {
                        Span<byte> rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaR = TiffUtils.ConvertToUIntLittleEndian(rowSpan);
                        r += deltaR;
                        BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, r);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaG = TiffUtils.ConvertToUIntLittleEndian(rowSpan);
                        g += deltaG;
                        BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, g);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaB = TiffUtils.ConvertToUIntLittleEndian(rowSpan);
                        b += deltaB;
                        BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, b);
                        offset += 4;

                        rowSpan = rowBytes.Slice(offset, 4);
                        uint deltaA = TiffUtils.ConvertToUIntLittleEndian(rowSpan);
                        a += deltaA;
                        BinaryPrimitives.WriteUInt32LittleEndian(rowSpan, a);
                        offset += 4;
                    }
                }
            }
        }
    }
}
