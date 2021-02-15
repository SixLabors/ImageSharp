// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression
{
    internal static class TiffDecompressorsFactory
    {
        public static TiffBaseDecompresor Create(
            TiffDecoderCompressionType method,
            MemoryAllocator allocator,
            TiffPhotometricInterpretation photometricInterpretation,
            int width,
            int bitsPerPixel,
            TiffPredictor predictor,
            FaxCompressionOptions faxOptions)
        {
            switch (method)
            {
                case TiffDecoderCompressionType.None:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                    return new NoneTiffCompression();

                case TiffDecoderCompressionType.PackBits:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                    return new PackBitsTiffCompression(allocator);

                case TiffDecoderCompressionType.Deflate:
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                    return new DeflateTiffCompression(allocator, width, bitsPerPixel, predictor);

                case TiffDecoderCompressionType.Lzw:
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "No fax compression options are expected");
                    return new LzwTiffCompression(allocator, width, bitsPerPixel, predictor);

                case TiffDecoderCompressionType.T4:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                    return new T4TiffCompression(allocator, faxOptions, photometricInterpretation, width);

                case TiffDecoderCompressionType.HuffmanRle:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                    return new ModifiedHuffmanTiffCompression(allocator, photometricInterpretation, width);

                default:
                    throw TiffThrowHelper.NotSupportedDecompressor(nameof(method));
            }
        }
    }
}
