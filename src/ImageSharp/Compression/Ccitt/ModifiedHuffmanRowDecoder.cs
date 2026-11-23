// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Decodes rows of 1-bit pixels from Modified Huffman data without EOL codes. Each row starts on a byte boundary of
/// the compressed data. See TIFF 6.0 specification, section 10, pages 43-44:
/// https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
/// </summary>
/// <remarks>
/// <para>
/// A row bit is 1 for a black pixel of the code and 0 for a white pixel. The photometric interpretation of the format
/// gives the displayed colors. For example, TIFF reverses the meaning of white and black for BlackIsZero data. See
/// TIFF 6.0 specification, section 10, page 44: https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
/// </para>
/// </remarks>
internal sealed class ModifiedHuffmanRowDecoder
{
    private CcittBitReader reader;

    private readonly int width;

    private bool isFirstRow = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModifiedHuffmanRowDecoder"/> class.
    /// </summary>
    /// <param name="stream">The compressed input stream.</param>
    /// <param name="bitOrder">The order of the code bits in each byte.</param>
    /// <param name="byteCount">The number of bytes to read from the stream.</param>
    /// <param name="width">The number of pixels in each row.</param>
    public ModifiedHuffmanRowDecoder(BufferedReadStream stream, CcittBitOrder bitOrder, int byteCount, int width)
    {
        this.reader = new CcittBitReader(stream, bitOrder, byteCount);
        this.width = width;
    }

    /// <summary>
    /// Decodes the next row. The first pixel goes to the most significant bit of the first byte. When the data ends
    /// before the row, the row is not changed.
    /// </summary>
    /// <param name="row">The destination row. It holds at least (width + 7) / 8 bytes, and all of its bits are 0.</param>
    public void DecodeRow(Span<byte> row)
    {
        // Each row after the first starts on the next byte boundary, so the reader skips the fill bits of the last row.
        if (!this.isFirstRow)
        {
            this.reader.AlignToByte();
        }

        this.isFirstRow = false;

        // The data has no EOL codes, but some writers put EOL codes before a row. The reader skips them, as the T.4
        // decoder does. No run code starts with 8 zero bits, so this changes no valid data.
        if (!this.reader.SkipEolCodes())
        {
            return;
        }

        int x = 0;
        bool isWhite = true;
        while (x < this.width)
        {
            // The row ends when it has all of its pixels. Fill bits or the end of the data end the row early. Its other
            // bits stay 0.
            int run = this.reader.ReadRun(isWhite, this.width - x);
            if (run < 0)
            {
                // The data has no code that starts a row, so the decoder cannot find the next row after data that is
                // not valid. Such data ends the data, and the rows after it stay 0.
                if (run == CcittBitReader.InvalidCode)
                {
                    this.reader.End();
                }

                return;
            }

            // The row starts with all bits 0, so only a black run writes to it.
            if (!isWhite)
            {
                BitWriterUtils.SetBits(row, x, run);
            }

            x += run;
            isWhite = !isWhite;
        }
    }
}
