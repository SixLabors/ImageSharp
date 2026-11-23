// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Encodes rows of pixels in the one-dimensional coding scheme of ITU-T T.4.
/// </summary>
/// <remarks>
/// Each row is a sequence of white and black runs that alternate, and the first run is white. A row that starts with
/// a black pixel starts with a white run of length 0. See ITU-T Recommendation T.4 (07/2003), section 4.1.1, pages 3-4:
/// https://www.itu.int/rec/T-REC-T.4
/// </remarks>
internal static class T4RowEncoder
{
    /// <summary>
    /// Writes the run codes of one row. The caller writes the EOL codes or the padding between the rows.
    /// </summary>
    /// <param name="writer">The bit writer.</param>
    /// <param name="row">The pixels of the row, one byte for each pixel.</param>
    /// <param name="white">The byte value of a white pixel. Each other value is a black pixel.</param>
    public static void EncodeRow(CcittBitWriter writer, ReadOnlySpan<byte> row, byte white)
    {
        int x = 0;
        bool isWhite = true;
        while (x < row.Length)
        {
            // The vectorized searches find the end of the run: the first pixel of the other color.
            ReadOnlySpan<byte> rest = row[x..];
            int run = isWhite ? rest.IndexOfAnyExcept(white) : rest.IndexOf(white);
            if (run < 0)
            {
                run = rest.Length;
            }

            writer.WriteRun(run, isWhite);
            x += run;
            isWhite = !isWhite;
        }
    }
}
