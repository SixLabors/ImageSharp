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

        // The parser accepts only the compressions in this switch. The default case shares the WebP case. Thus each path returns a decompressor.
        switch (method)
        {
            case TiffDecoderCompressionType.None:
                return new NoneTiffCompression(allocator, width, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.PackBits:
                return new PackBitsTiffCompression(allocator, width, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.Deflate:
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
                return new T4TiffCompression(allocator, GetCcittBitOrder(fillOrder), segmentWidth, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.T6:
                return new T6TiffCompression(allocator, GetCcittBitOrder(fillOrder), segmentWidth, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.HuffmanRle:
                return new ModifiedHuffmanTiffCompression(allocator, GetCcittBitOrder(fillOrder), segmentWidth, bitsPerPixel, options.SegmentIntegrityHandling);

            case TiffDecoderCompressionType.Jpeg:
                return new JpegTiffCompression(
                    new(options), allocator, segmentWidth, bitsPerPixel, metadata, jpegTables, photometricInterpretation);

            case TiffDecoderCompressionType.OldJpeg:
                return new OldJpegTiffCompression(
                    new(options), allocator, segmentWidth, bitsPerPixel, metadata, oldJpegStartOfImageMarker, photometricInterpretation);

            case TiffDecoderCompressionType.Webp:
            default:
                DebugGuard.IsTrue(method == TiffDecoderCompressionType.Webp, "The parser accepts no other compression.");
                return new WebpTiffCompression(options, allocator, segmentWidth, bitsPerPixel, metadata);
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
