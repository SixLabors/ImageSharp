// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Ccitt;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression;

internal static class TiffDecompressorsFactory
{
    public static TiffBaseDecompressor Create(
        DecoderOptions options,
        TiffDecoderCompressionType method,
        MemoryAllocator allocator,
        TiffPhotometricInterpretation photometricInterpretation,
        int width,
        int bitsPerPixel,
        ImageFrameMetadata metadata,
        TiffColorType colorType,
        TiffPredictor predictor,
        FaxCompressionOptions faxOptions,
        byte[] jpegTables,
        uint oldJpegStartOfImageMarker,
        TiffFillOrder fillOrder,
        ByteOrder byteOrder,
        bool isTiled = false,
        int tileWidth = 0,
        int tileHeight = 0)
    {
        // Fax rows and nested image headers describe the full tile width, including padding outside the visible image.
        int segmentWidth = isTiled ? tileWidth : width;

        switch (method)
        {
            case TiffDecoderCompressionType.None:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                return new NoneTiffCompression(allocator, width, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.PackBits:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                return new PackBitsTiffCompression(allocator, width, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.Deflate:
                DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");

                return new DeflateTiffCompression(
                    allocator,
                    width,
                    bitsPerPixel,
                    colorType,
                    predictor,
                    byteOrder == ByteOrder.BigEndian,
                    isTiled,
                    tileWidth,
                    tileHeight,
                    options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.Lzw:
                DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");

                return new LzwTiffCompression(
                    allocator,
                    width,
                    bitsPerPixel,
                    colorType,
                    predictor,
                    byteOrder == ByteOrder.BigEndian,
                    isTiled,
                    tileWidth,
                    tileHeight,
                    options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.T4:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new T4TiffCompression(allocator, GetCcittBitOrder(fillOrder), segmentWidth, bitsPerPixel, faxOptions, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.T6:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new T6TiffCompression(allocator, GetCcittBitOrder(fillOrder), segmentWidth, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.HuffmanRle:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new ModifiedHuffmanTiffCompression(allocator, GetCcittBitOrder(fillOrder), segmentWidth, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.Jpeg:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new JpegTiffCompression(
                    new(options), allocator, segmentWidth, bitsPerPixel, metadata, jpegTables, photometricInterpretation);

            case TiffDecoderCompressionType.OldJpeg:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new OldJpegTiffCompression(
                    new(options), allocator, segmentWidth, bitsPerPixel, metadata, oldJpegStartOfImageMarker, photometricInterpretation);

            case TiffDecoderCompressionType.Webp:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new WebpTiffCompression(options, allocator, segmentWidth, bitsPerPixel, metadata);

            default:
                throw TiffThrowHelper.NotSupportedDecompressor(nameof(method));
        }
    }

    /// <summary>
    /// Gets the CCITT bit order for a TIFF fill order. A fill order of 2 stores the first pixel in the low-order bit.
    /// Any other value uses the default fill order 1, which stores the first pixel in the high-order bit. See TIFF 6.0
    /// specification, section 8, FillOrder, page 32: https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
    /// </summary>
    /// <param name="fillOrder">The TIFF fill order.</param>
    /// <returns>The CCITT bit order.</returns>
    private static CcittBitOrder GetCcittBitOrder(TiffFillOrder fillOrder)
        => fillOrder == TiffFillOrder.LeastSignificantBitFirst
            ? CcittBitOrder.LeastSignificantBitFirst
            : CcittBitOrder.MostSignificantBitFirst;
}
