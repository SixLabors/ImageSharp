// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Encodes rows of palette indices as BI_RLE8 or BI_RLE4 data.
/// </summary>
/// <remarks>
/// <para>
/// The format is defined in Bitmap Compression (Microsoft):
/// <see href="https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-compression"/>.
/// RLE data is a sequence of two-byte commands. The commands that the encoder writes are:
/// </para>
/// <list type="bullet">
/// <item><description>a &gt; 0, b: an encoded run of a pixels. RLE8: each pixel has index b. RLE4: the pixels
/// alternate between the high nibble and the low nibble of b, first the high nibble.</description></item>
/// <item><description>0, b &gt; 2: an absolute run of b indices. RLE8 stores one index in each byte. RLE4 stores two
/// indices in each byte, the first in the high nibble. The run data has an even byte count, so a zero byte pads odd
/// data.</description></item>
/// <item><description>0, 0: the end of a row. 0, 1: the end of the bitmap.</description></item>
/// </list>
/// <para>
/// Each command holds at most 255 pixels. An absolute run must hold at least 3 pixels, because the values 0, 1 and 2
/// after a zero byte are escape codes. Shorter segments use encoded runs of one or two pixels.
/// </para>
/// </remarks>
internal static class BmpRleEncoder
{
    /// <summary>
    /// The largest pixel count of one command.
    /// </summary>
    private const int MaxRunLength = 255;

    /// <summary>
    /// The smallest RLE8 run that splits an absolute run. Inside the absolute run, the run costs 1 byte for each index.
    /// The split costs 4 bytes: 2 for the encoded run and 2 for the header of the next absolute run. The padding bytes
    /// of the two absolute runs add 0.5 bytes on average. A run of 5 costs more than 4.5 bytes, so it saves space.
    /// </summary>
    private const int Rle8SplitRun = 5;

    /// <summary>
    /// The smallest RLE4 pair run that splits an absolute run. Inside the absolute run, the run costs 0.5 bytes for
    /// each index. The split costs 4.5 bytes on average, as for <see cref="Rle8SplitRun"/>. A run of 10 costs 5 bytes,
    /// so it saves space.
    /// </summary>
    private const int Rle4SplitRun = 10;

    /// <summary>
    /// Gets the largest byte count of one encoded row, with its end command.
    /// </summary>
    /// <param name="width">The pixel count of the row.</param>
    /// <returns>The byte count.</returns>
    /// <remarks>
    /// An encoded run of one pixel is the most expensive command: 2 bytes for each pixel. An absolute run has 3 or more
    /// pixels. A run of n pixels has 2 bytes of command, at most n bytes of data and 1 byte of padding. Thus it is also
    /// at most 2 bytes for each pixel. The end command adds 2 bytes.
    /// </remarks>
    public static int GetMaxRowLength(int width) => (2 * width) + 2;

    /// <summary>
    /// Encodes one row and writes it to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="indices">The palette indices of the row, one byte each. Each index fits in <paramref name="bitsPerIndex"/> bits.</param>
    /// <param name="buffer">The scratch buffer. It holds at least <see cref="GetMaxRowLength(int)"/> bytes.</param>
    /// <param name="bitsPerIndex">The bit count of one index: 4 for RLE4 or 8 for RLE8.</param>
    /// <param name="isLastRow">Whether this row is the last row in the stream. It ends with the end of the bitmap.</param>
    public static void WriteRow(Stream stream, ReadOnlySpan<byte> indices, Span<byte> buffer, int bitsPerIndex, bool isLastRow)
    {
        int length = bitsPerIndex == 8 ? EncodeRle8(indices, buffer) : EncodeRle4(indices, buffer);

        // The last row ends the bitmap. Each other row ends with the end of a row. The 8-bit example in Bitmap Compression
        // also ends its last row with the end of the bitmap and no end of a row.
        buffer[length++] = 0;
        buffer[length++] = isLastRow ? (byte)1 : (byte)0;
        stream.Write(buffer[..length]);
    }

    /// <summary>
    /// Encodes one row of 8-bit indices as RLE8 commands.
    /// </summary>
    /// <param name="indices">The indices of the row.</param>
    /// <param name="destination">The destination of the commands.</param>
    /// <returns>The byte count of the commands.</returns>
    private static int EncodeRle8(ReadOnlySpan<byte> indices, Span<byte> destination)
    {
        int length = 0;
        int i = 0;
        while (i < indices.Length)
        {
            // A run of 3 or more equal indices is an encoded run: 2 bytes. IndexOfAnyExcept finds the end of the run
            // with vector operations.
            int run = GetRun8(indices, i);
            if (run >= 3)
            {
                destination[length++] = (byte)run;
                destination[length++] = indices[i];
                i += run;
                continue;
            }

            // Collect indices into one segment. A run of equal indices ends the segment only when an encoded run makes
            // the row smaller. Inside a segment of 3 or more indices, a run of r indices costs r bytes. A split costs
            // 2 bytes for the encoded run and 2 bytes for the header of the next absolute run. Thus a run must be at
            // least Rle8SplitRun indices. A run at the end of the row has no next absolute run. A segment of 1 or 2
            // indices has no absolute run to split. In these two cases, a run of 3 is enough.
            int end = i + 1;
            while (end < indices.Length && end - i < MaxRunLength)
            {
                if (end + 2 < indices.Length && indices[end] == indices[end + 1] && indices[end] == indices[end + 2])
                {
                    int split = GetRun8(indices, end);
                    if (end - i < 3 || split >= Rle8SplitRun || end + split == indices.Length)
                    {
                        break;
                    }

                    // Keep the run in the segment. The index after the run is different, so the scan continues there.
                    end = Math.Min(end + split, i + MaxRunLength);
                    continue;
                }

                end++;
            }

            int count = end - i;
            if (count >= 3)
            {
                // An absolute run stores the indices. Its data must end on a 2-byte boundary.
                destination[length++] = 0;
                destination[length++] = (byte)count;
                indices.Slice(i, count).CopyTo(destination[length..]);
                length += count;
                if ((count & 1) != 0)
                {
                    destination[length++] = 0;
                }
            }
            else
            {
                // An absolute run cannot hold 1 or 2 indices. Write an encoded run for each group of equal indices.
                for (int k = i; k < end;)
                {
                    int groupLength = Math.Min(GetRun8(indices, k), end - k);
                    destination[length++] = (byte)groupLength;
                    destination[length++] = indices[k];
                    k += groupLength;
                }
            }

            i = end;
        }

        return length;
    }

    /// <summary>
    /// Encodes one row of 4-bit indices as RLE4 commands.
    /// </summary>
    /// <param name="indices">The indices of the row, one byte each.</param>
    /// <param name="destination">The destination of the commands.</param>
    /// <returns>The byte count of the commands.</returns>
    private static int EncodeRle4(ReadOnlySpan<byte> indices, Span<byte> destination)
    {
        int length = 0;
        int i = 0;
        while (i < indices.Length)
        {
            // An encoded RLE4 run repeats a pair of indices: a b a b. A run of 4 or more pixels is 2 bytes, which is
            // not more than the absolute data of the same pixels.
            int run = GetRun4(indices, i);
            if (run >= 4)
            {
                destination[length++] = (byte)run;
                destination[length++] = (byte)((indices[i] << 4) | indices[i + 1]);
                i += run;
                continue;
            }

            // Collect indices into one segment. A pair run of 4 or more starts at index j when indices j + 2 and j + 3
            // repeat indices j and j + 1. The run ends the segment only when an encoded run makes the row smaller.
            // Inside a segment of 3 or more indices, a run of r indices costs r / 2 bytes. A split costs 2 bytes for the
            // encoded run and 2 bytes for the header of the next absolute run. Thus a run must be at least Rle4SplitRun
            // indices. A run at the end of the row has no next absolute run. A segment of 1 or 2 indices has no
            // absolute run to split. In these two cases, a run of 4 is enough.
            int end = i + 1;
            while (end < indices.Length && end - i < MaxRunLength)
            {
                if (end + 3 < indices.Length && indices[end + 2] == indices[end] && indices[end + 3] == indices[end + 1])
                {
                    int split = GetRun4(indices, end);
                    if (end - i < 3 || split >= Rle4SplitRun || end + split == indices.Length)
                    {
                        break;
                    }

                    // Keep the run in the segment. The index after the run does not repeat the index two places
                    // before it. Thus the scan continues at the last index of the run, which can start a new pair run.
                    end = Math.Min(end + split - 1, i + MaxRunLength);
                    continue;
                }

                end++;
            }

            int count = end - i;
            if (count >= 3)
            {
                // An absolute run packs two indices in each byte, the first in the high nibble. The packer writes
                // (count + 1) / 2 bytes. The data must end on a 2-byte boundary.
                destination[length++] = 0;
                destination[length++] = (byte)count;
                int dataLength = (count + 1) >> 1;
                EncodingUtilities.PackIndices(indices.Slice(i, count), destination[length..], 4);
                length += dataLength;
                if ((dataLength & 1) != 0)
                {
                    destination[length++] = 0;
                }
            }
            else
            {
                // An absolute run cannot hold 1 or 2 indices. One encoded run holds any 2 indices as a pair. The low
                // nibble of a single index is not used.
                destination[length++] = (byte)count;
                destination[length++] = count == 2 ? (byte)((indices[i] << 4) | indices[i + 1]) : (byte)(indices[i] << 4);
            }

            i = end;
        }

        return length;
    }

    /// <summary>
    /// Gets the length of the run of equal indices at <paramref name="start"/>.
    /// </summary>
    /// <param name="indices">The indices of the row.</param>
    /// <param name="start">The first index of the run.</param>
    /// <returns>The run length: 1 to <see cref="MaxRunLength"/>.</returns>
    private static int GetRun8(ReadOnlySpan<byte> indices, int start)
    {
        ReadOnlySpan<byte> window = indices.Slice(start, Math.Min(MaxRunLength, indices.Length - start));
        int run = window.IndexOfAnyExcept(window[0]);
        return run < 0 ? window.Length : run;
    }

    /// <summary>
    /// Gets the length of the pair run at <paramref name="start"/>: a b a b, with each index equal to the index two
    /// places before it.
    /// </summary>
    /// <param name="indices">The indices of the row.</param>
    /// <param name="start">The first index of the run.</param>
    /// <returns>The run length: 1 to <see cref="MaxRunLength"/>.</returns>
    private static int GetRun4(ReadOnlySpan<byte> indices, int start)
    {
        int limit = Math.Min(MaxRunLength, indices.Length - start);

        // Any one or two indices make a pair run.
        if (limit <= 2)
        {
            return limit;
        }

        // Each index after the first two must equal the index two places before it. The common prefix of the row from
        // start + 2 and the row from start counts those indices, with the vectorized compare of the span.
        ReadOnlySpan<byte> run = indices.Slice(start, limit);
        return 2 + run[2..].CommonPrefixLength(run[..(limit - 2)]);
    }
}
