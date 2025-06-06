// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
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
        switch (method)
        {
            case TiffDecoderCompressionType.None:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                return new NoneTiffCompression(allocator, width, bitsPerPixel);

            case TiffDecoderCompressionType.PackBits:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                return new PackBitsTiffCompression(allocator, width, bitsPerPixel);

            case TiffDecoderCompressionType.Deflate:
                DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                return new DeflateTiffCompression(allocator, width, bitsPerPixel, colorType, predictor, byteOrder == ByteOrder.BigEndian, isTiled, tileWidth, tileHeight);

            case TiffDecoderCompressionType.Lzw:
                DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                return new LzwTiffCompression(allocator, width, bitsPerPixel, colorType, predictor, byteOrder == ByteOrder.BigEndian, isTiled, tileWidth, tileHeight);

            case TiffDecoderCompressionType.T4:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new T4TiffCompression(allocator, fillOrder, width, bitsPerPixel, faxOptions, photometricInterpretation);

            case TiffDecoderCompressionType.T6:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new T6TiffCompression(allocator, fillOrder, width, bitsPerPixel, photometricInterpretation);

            case TiffDecoderCompressionType.HuffmanRle:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new ModifiedHuffmanTiffCompression(allocator, fillOrder, width, bitsPerPixel, photometricInterpretation);

            case TiffDecoderCompressionType.Jpeg:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new JpegTiffCompression(new() { GeneralOptions = options }, allocator, width, bitsPerPixel, metadata, jpegTables, photometricInterpretation);

            case TiffDecoderCompressionType.OldJpeg:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new OldJpegTiffCompression(new() { GeneralOptions = options }, allocator, width, bitsPerPixel, metadata, oldJpegStartOfImageMarker, photometricInterpretation);

            case TiffDecoderCompressionType.Webp:
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new WebpTiffCompression(options, allocator, width, bitsPerPixel);

            default:
                throw TiffThrowHelper.NotSupportedDecompressor(nameof(method));
        }
    }
}
