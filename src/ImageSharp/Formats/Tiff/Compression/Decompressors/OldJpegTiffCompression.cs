// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

internal sealed class OldJpegTiffCompression : JpegTiffCompression
{
    private readonly uint startOfImageMarker;

    public OldJpegTiffCompression(
        MemoryAllocator memoryAllocator,
        int width,
        int bitsPerPixel,
        JpegDecoderOptions options,
        uint startOfImageMarker,
        TiffPhotometricInterpretation photometricInterpretation)
        : base(memoryAllocator, width, bitsPerPixel, options, photometricInterpretation) => this.startOfImageMarker = startOfImageMarker;

    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        stream.Position = this.startOfImageMarker;

        this.DecodeJpegData(stream, buffer, false, cancellationToken);
    }
}
