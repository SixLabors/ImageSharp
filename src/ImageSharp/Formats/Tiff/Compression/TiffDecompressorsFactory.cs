// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    internal static class TiffDecompressorsFactory
    {
        public static TiffBaseDecompressor Create(
            TiffDecoderCompressionType method,
            MemoryAllocator allocator,
            TiffPhotometricInterpretation photometricInterpretation,
            int width,
            int bitsPerPixel,
            TiffPredictor predictor,
            FaxCompressionOptions faxOptions,
            ByteOrder byteOrder)
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
                    return new DeflateTiffCompression(allocator, width, bitsPerPixel, predictor, byteOrder == ByteOrder.BigEndian);

                case TiffDecoderCompressionType.Lzw:
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                    return new LzwTiffCompression(allocator, width, bitsPerPixel, predictor, byteOrder == ByteOrder.BigEndian);

                case TiffDecoderCompressionType.T4:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                    return new T4TiffCompression(allocator, width, bitsPerPixel, faxOptions, photometricInterpretation);

                case TiffDecoderCompressionType.HuffmanRle:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                    return new ModifiedHuffmanTiffCompression(allocator, width, bitsPerPixel, photometricInterpretation);

                default:
                    throw TiffThrowHelper.NotSupportedDecompressor(nameof(method));
            }
        }
    }
}
