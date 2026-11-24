// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Decodes rows of 1-bit pixels from data in the one-dimensional coding scheme of ITU-T T.4.
/// </summary>
/// <remarks>
/// An EOL code comes before the first row and after each row. Fill bits can come before each EOL code. Each row is a
/// sequence of white and black runs that alternate, and the first run is white. See ITU-T Recommendation T.4 (07/2003),
/// section 4.1, pages 3-4: https://www.itu.int/rec/T-REC-T.4
/// <para>
/// A row bit is 1 for a black pixel of the code and 0 for a white pixel. The photometric interpretation of the format
/// gives the displayed colors. For example, TIFF reverses the meaning of white and black for BlackIsZero data. See
/// TIFF 6.0 specification, section 10, page 44: https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
/// </para>
/// <para>
/// The decoder does not throw for data that is not valid. A code that is not valid ends the row. A run that is longer
/// than the row also ends the row. The decoder then continues after the next EOL code and sets
/// <see cref="IsDamaged"/>. Thus the caller can apply the segment integrity mode of the decode.
/// </para>
/// </remarks>
internal sealed class T4RowDecoder
{
    private CcittBitReader reader;

    private readonly int width;

    /// <summary>
    /// Initializes a new instance of the <see cref="T4RowDecoder"/> class.
    /// </summary>
    /// <param name="stream">The compressed input stream.</param>
    /// <param name="bitOrder">The order of the code bits in each byte.</param>
    /// <param name="byteCount">The number of bytes to read from the stream.</param>
    /// <param name="width">The number of pixels in each row.</param>
    public T4RowDecoder(BufferedReadStream stream, CcittBitOrder bitOrder, int byteCount, int width)
    {
        this.reader = new CcittBitReader(stream, bitOrder, byteCount);
        this.width = width;
    }

    /// <summary>
    /// Gets a value indicating whether the data is damaged. Damaged data has a code that is not valid, a row with fewer
    /// pixels than the width, or an end before a row.
    /// </summary>
    public bool IsDamaged { get; private set; }

    /// <summary>
    /// Decodes the next row. The first pixel goes to the most significant bit of the first byte.
    /// </summary>
    /// <param name="row">The destination row. It holds at least (width + 7) / 8 bytes, and all of its bits are 0.</param>
    /// <returns>
    /// <see langword="true"/> if the data has codes for the row. <see langword="false"/> if the data ended before the
    /// row. Then the row is not changed.
    /// </returns>
    public bool DecodeRow(Span<byte> row)
    {
        // The EOL code before the row, and the fill bits before it, separate the row from the row above.
        if (!this.reader.SkipEolCodes(out bool skippedRow))
        {
            this.IsDamaged = true;
            return false;
        }

        // The row starts with bits that are no code. The reader skipped the row data up to the next EOL code. Thus this
        // row stays 0, and the next row decodes into its own place.
        if (skippedRow)
        {
            this.IsDamaged = true;
            return true;
        }

        // An EOL code, fill bits or the end of the data ends the row early. Its other bits stay 0.
        int result = this.reader.ReadRow(row, this.width);
        if (result < 0)
        {
            // The row has fewer pixels than the width, so the data is damaged. Data that is not valid also ends the
            // row. The next EOL code starts the next row. Thus the decoder skips to it, as a JPEG decoder skips to the
            // next restart marker.
            if (result == CcittBitReader.InvalidCode)
            {
                this.reader.SkipToEol();
            }

            this.IsDamaged = true;
        }

        return true;
    }
}
