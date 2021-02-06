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
                    DebugGuard.Equals(predictor, TiffPredictor.None);
                    DebugGuard.Equals(faxOptions, FaxCompressionOptions.None);
                    return new NoneTiffCompression();

                case TiffDecoderCompressionType.PackBits:
                    DebugGuard.Equals(predictor, TiffPredictor.None);
                    DebugGuard.Equals(faxOptions, FaxCompressionOptions.None);
                    return new PackBitsTiffCompression(allocator);

                case TiffDecoderCompressionType.Deflate:
                    DebugGuard.Equals(faxOptions, FaxCompressionOptions.None);
                    return new DeflateTiffCompression(allocator, width, bitsPerPixel, predictor);

                case TiffDecoderCompressionType.Lzw:
                    DebugGuard.Equals(faxOptions, FaxCompressionOptions.None);
                    return new LzwTiffCompression(allocator, width, bitsPerPixel, predictor);

                case TiffDecoderCompressionType.T4:
                    DebugGuard.Equals(predictor, TiffPredictor.None);
                    DebugGuard.Equals(faxOptions, FaxCompressionOptions.None);
                    return new T4TiffCompression(allocator, faxOptions, photometricInterpretation, width);

                case TiffDecoderCompressionType.HuffmanRle:
                    DebugGuard.Equals(predictor, TiffPredictor.None);
                    DebugGuard.Equals(faxOptions, FaxCompressionOptions.None);
                    return new ModifiedHuffmanTiffCompression(allocator, photometricInterpretation, width);

                default:
                    throw TiffThrowHelper.NotSupportedDecompressor(nameof(method));
            }
        }
    }
}
