// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Decompressors
{
    internal static class TiffDecompressorsFactory
    {
        public static TiffBaseCompression Create(
            TiffDecoderCompressionType compressionType,
            MemoryAllocator allocator,
            TiffPhotometricInterpretation photometricInterpretation,
            int width,
            int bitsPerPixel,
            TiffPredictor predictor,
            FaxCompressionOptions faxOptions)
        {
            switch (compressionType)
            {
                case TiffDecoderCompressionType.None:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "predictor");
                    return new NoneTiffCompression(allocator);

                case TiffDecoderCompressionType.PackBits:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "predictor");
                    return new PackBitsTiffCompression(allocator);

                case TiffDecoderCompressionType.Deflate:
                    return new DeflateTiffCompression(allocator, width, bitsPerPixel, predictor);

                case TiffDecoderCompressionType.Lzw:
                    return new LzwTiffCompression(allocator, width, bitsPerPixel, predictor);

                case TiffDecoderCompressionType.T4:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "predictor");
                    return new T4TiffCompression(allocator, faxOptions, photometricInterpretation, width);

                case TiffDecoderCompressionType.HuffmanRle:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "predictor");
                    return new ModifiedHuffmanTiffCompression(allocator, photometricInterpretation, width);

                default:
                    throw TiffThrowHelper.NotSupportedCompression(nameof(compressionType));
            }
        }
    }
}
