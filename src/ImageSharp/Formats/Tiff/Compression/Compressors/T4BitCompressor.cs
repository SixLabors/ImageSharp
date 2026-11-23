// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Ccitt;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

/// <summary>
/// Bitwriter for writing compressed CCITT T4 1D data.
/// </summary>
internal sealed class T4BitCompressor : TiffCcittCompressor
{
    /// <summary>
    /// Whether the encoder writes Modified Huffman data (TIFF compression 2): T.4 run codes without EOL codes, with each
    /// row padded to a byte boundary.
    /// </summary>
    private readonly bool useModifiedHuffman;

    /// <summary>
    /// Initializes a new instance of the <see cref="T4BitCompressor" /> class.
    /// </summary>
    /// <param name="output">The output stream to write the compressed data.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="useModifiedHuffman">Indicates if the modified huffman RLE should be used.</param>
    public T4BitCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel, bool useModifiedHuffman = false)
        : base(output, allocator, width, bitsPerPixel) => this.useModifiedHuffman = useModifiedHuffman;

    /// <inheritdoc/>
    public override TiffCompression Method => this.useModifiedHuffman ? TiffCompression.Ccitt1D : TiffCompression.CcittGroup3Fax;

    /// <inheritdoc/>
    protected override void EncodeStrip(ReadOnlySpan<byte> pixelsAsGray, int height)
    {
        CcittBitWriter writer = this.Writer;
        if (!this.useModifiedHuffman)
        {
            // An EOL code is expected at the start of the data.
            writer.WriteEol();
        }

        for (int y = 0; y < height; y++)
        {
            T4RowEncoder.EncodeRow(writer, pixelsAsGray.Slice(y * this.Width, this.Width), 255);

            // Modified Huffman rows end on a byte boundary. T4 rows end with an EOL code.
            if (this.useModifiedHuffman)
            {
                writer.PadToByte();
            }
            else
            {
                writer.WriteEol();
            }
        }
    }
}
