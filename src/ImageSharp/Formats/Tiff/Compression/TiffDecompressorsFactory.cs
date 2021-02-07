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
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Values must be equals");
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "Values must be equals");
                    return new NoneTiffCompression();

                case TiffDecoderCompressionType.PackBits:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Values must be equals");
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "Values must be equals");
                    return new PackBitsTiffCompression(allocator);

                case TiffDecoderCompressionType.Deflate:
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "Values must be equals");
                    return new DeflateTiffCompression(allocator, width, bitsPerPixel, predictor);

                case TiffDecoderCompressionType.Lzw:
                    DebugGuard.IsTrue(faxOptions == FaxCompressionOptions.None, "Values must be equals");
                    return new LzwTiffCompression(allocator, width, bitsPerPixel, predictor);

                case TiffDecoderCompressionType.T4:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Values must be equals");
                    return new T4TiffCompression(allocator, faxOptions, photometricInterpretation, width);

                case TiffDecoderCompressionType.HuffmanRle:
                    DebugGuard.IsTrue(predictor == TiffPredictor.None, "Values must be equals");
                    return new ModifiedHuffmanTiffCompression(allocator, photometricInterpretation, width);

                default:
                    throw TiffThrowHelper.NotSupportedDecompressor(nameof(method));
            }
        }
    }
}
