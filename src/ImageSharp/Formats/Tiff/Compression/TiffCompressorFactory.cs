// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression;

internal static class TiffCompressorFactory
{
    /// <summary>
    /// Creates a strip compressor that uses the TIFF encoder's configuration and memory allocator.
    /// </summary>
    /// <param name="method">The strip compression method.</param>
    /// <param name="output">The destination TIFF stream.</param>
    /// <param name="configuration">The configuration shared with embedded codecs.</param>
    /// <param name="width">The strip width in pixels.</param>
    /// <param name="bitsPerPixel">The number of stored bits per pixel.</param>
    /// <param name="compressionLevel">The Deflate compression level.</param>
    /// <param name="predictor">The sample predictor.</param>
    /// <returns>The compressor for the selected method.</returns>
    public static TiffBaseCompressor Create(
        TiffCompression method,
        Stream output,
        Configuration configuration,
        int width,
        int bitsPerPixel,
        DeflateCompressionLevel compressionLevel,
        TiffPredictor predictor)
    {
        MemoryAllocator allocator = configuration.MemoryAllocator;

        switch (method)
        {
            // The following compression types are not implemented in the encoder and will default to no compression instead.
            case TiffCompression.ItuTRecT43:
            case TiffCompression.ItuTRecT82:
            case TiffCompression.OldJpeg:
            case TiffCompression.OldDeflate:
            case TiffCompression.None:
                DebugGuard.IsTrue(compressionLevel == DeflateCompressionLevel.DefaultCompression, "No deflate compression level is expected to be set");
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");

                return new NoCompressor(output, allocator, width, bitsPerPixel);

            case TiffCompression.Jpeg:
                DebugGuard.IsTrue(compressionLevel == DeflateCompressionLevel.DefaultCompression, "No deflate compression level is expected to be set");
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new TiffJpegCompressor(output, configuration, width, bitsPerPixel);

            case TiffCompression.PackBits:
                DebugGuard.IsTrue(compressionLevel == DeflateCompressionLevel.DefaultCompression, "No deflate compression level is expected to be set");
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new PackBitsCompressor(output, allocator, width, bitsPerPixel);

            case TiffCompression.Deflate:
                return new DeflateCompressor(output, allocator, width, bitsPerPixel, predictor, compressionLevel);

            case TiffCompression.Lzw:
                DebugGuard.IsTrue(compressionLevel == DeflateCompressionLevel.DefaultCompression, "No deflate compression level is expected to be set");
                return new LzwCompressor(output, allocator, width, bitsPerPixel, predictor);

            case TiffCompression.CcittGroup3Fax:
                DebugGuard.IsTrue(compressionLevel == DeflateCompressionLevel.DefaultCompression, "No deflate compression level is expected to be set");
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new T4BitCompressor(output, allocator, width, bitsPerPixel, false);

            case TiffCompression.CcittGroup4Fax:
                DebugGuard.IsTrue(compressionLevel == DeflateCompressionLevel.DefaultCompression, "No deflate compression level is expected to be set");
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new T6BitCompressor(output, allocator, width, bitsPerPixel);

            case TiffCompression.Ccitt1D:
                DebugGuard.IsTrue(compressionLevel == DeflateCompressionLevel.DefaultCompression, "No deflate compression level is expected to be set");
                DebugGuard.IsTrue(predictor == TiffPredictor.None, "Predictor should only be used with lzw or deflate compression");
                return new T4BitCompressor(output, allocator, width, bitsPerPixel, true);

            default:
                throw TiffThrowHelper.NotSupportedCompressor(method.ToString());
        }
    }
}
