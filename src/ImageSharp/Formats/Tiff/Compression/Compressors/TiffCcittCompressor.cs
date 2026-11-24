// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Ccitt;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

/// <summary>
/// Common functionality for CCITT T4 and T6 Compression
/// </summary>
internal abstract class TiffCcittCompressor : TiffBaseCompressor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffCcittCompressor" /> class.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="allocator">The allocator.</param>
    /// <param name="width">The width.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    protected TiffCcittCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel)
        : base(output, allocator, width, bitsPerPixel)
    {
        DebugGuard.IsTrue(bitsPerPixel == 1, nameof(bitsPerPixel), "CCITT compression requires one bit per pixel");
        this.Writer = new CcittBitWriter(output, allocator);
    }

    /// <summary>
    /// Gets the bit writer. It writes the code bits of each strip to <see cref="TiffBaseCompressor.Output"/>.
    /// </summary>
    protected CcittBitWriter Writer { get; }

    /// <inheritdoc/>
    public override void Initialize(int rowsPerStrip)
    {
    }

    /// <summary>
    /// Writes a strip compressed with CCITT T4 or T6 to the stream.
    /// </summary>
    /// <param name="rows">The pixels as 8-bit gray array.</param>
    /// <param name="height">The strip height.</param>
    public override void CompressStrip(Span<byte> rows, int height)
    {
        DebugGuard.IsTrue(rows.Length / height == this.Width, "Values must be equals");
        DebugGuard.IsTrue(rows.Length % height == 0, "Values must be equals");

        this.EncodeStrip(rows, height);

        // Each strip ends on a byte boundary, so the next strip starts on its own byte.
        this.Writer.Flush();
    }

    /// <summary>
    /// Writes the code bits of a strip.
    /// </summary>
    /// <param name="pixelsAsGray">The pixels as 8-bit gray array. A white pixel is 255, and a black pixel is 0.</param>
    /// <param name="height">The strip height.</param>
    protected abstract void EncodeStrip(ReadOnlySpan<byte> pixelsAsGray, int height);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.Writer.Dispose();
}
