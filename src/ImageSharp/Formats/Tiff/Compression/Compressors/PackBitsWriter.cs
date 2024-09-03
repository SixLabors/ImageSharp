// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

/// <summary>
/// Pack Bits compression for tiff images. See Tiff Spec v6, section 9.
/// </summary>
internal static class PackBitsWriter
{
    public static int PackBits(ReadOnlySpan<byte> rowSpan, Span<byte> compressedRowSpan)
    {
        int maxRunLength = 127;
        int posInRowSpan = 0;
        int bytesWritten = 0;
        int literalRunLength = 0;

        while (posInRowSpan < rowSpan.Length)
        {
            bool useReplicateRun = IsReplicateRun(rowSpan, posInRowSpan);
            if (useReplicateRun)
            {
                if (literalRunLength > 0)
                {
                    WriteLiteralRun(rowSpan, posInRowSpan, literalRunLength, compressedRowSpan, bytesWritten);
                    bytesWritten += literalRunLength + 1;
                }

                // Write a run with the same bytes.
                int runLength = FindRunLength(rowSpan, posInRowSpan, maxRunLength);
                WriteRun(rowSpan, posInRowSpan, runLength, compressedRowSpan, bytesWritten);

                bytesWritten += 2;
                literalRunLength = 0;
                posInRowSpan += runLength;
                continue;
            }

            literalRunLength++;
            posInRowSpan++;

            if (literalRunLength >= maxRunLength)
            {
                WriteLiteralRun(rowSpan, posInRowSpan, literalRunLength, compressedRowSpan, bytesWritten);
                bytesWritten += literalRunLength + 1;
                literalRunLength = 0;
            }
        }

        if (literalRunLength > 0)
        {
            WriteLiteralRun(rowSpan, posInRowSpan, literalRunLength, compressedRowSpan, bytesWritten);
            bytesWritten += literalRunLength + 1;
        }

        return bytesWritten;
    }

    private static void WriteLiteralRun(ReadOnlySpan<byte> rowSpan, int end, int literalRunLength, Span<byte> compressedRowSpan, int compressedRowPos)
    {
        DebugGuard.MustBeLessThanOrEqualTo(literalRunLength, 127, nameof(literalRunLength));

        int literalRunStart = end - literalRunLength;
        sbyte runLength = (sbyte)(literalRunLength - 1);
        compressedRowSpan[compressedRowPos] = (byte)runLength;
        rowSpan.Slice(literalRunStart, literalRunLength).CopyTo(compressedRowSpan[(compressedRowPos + 1)..]);
    }

    private static void WriteRun(ReadOnlySpan<byte> rowSpan, int start, int runLength, Span<byte> compressedRowSpan, int compressedRowPos)
    {
        DebugGuard.MustBeLessThanOrEqualTo(runLength, 127, nameof(runLength));

        sbyte headerByte = (sbyte)(-runLength + 1);
        compressedRowSpan[compressedRowPos] = (byte)headerByte;
        compressedRowSpan[compressedRowPos + 1] = rowSpan[start];
    }

    private static bool IsReplicateRun(ReadOnlySpan<byte> rowSpan, int startPos)
    {
        // We consider run which has at least 3 same consecutive bytes a candidate for a run.
        byte startByte = rowSpan[startPos];
        int count = 0;
        for (int i = startPos + 1; i < rowSpan.Length; i++)
        {
            if (rowSpan[i] == startByte)
            {
                count++;
                if (count >= 2)
                {
                    return true;
                }
            }
            else
            {
                break;
            }
        }

        return false;
    }

    private static int FindRunLength(ReadOnlySpan<byte> rowSpan, int startPos, int maxRunLength)
    {
        byte startByte = rowSpan[startPos];
        int count = 1;
        for (int i = startPos + 1; i < rowSpan.Length; i++)
        {
            if (rowSpan[i] == startByte)
            {
                count++;
            }
            else
            {
                break;
            }

            if (count == maxRunLength)
            {
                break;
            }
        }

        return count;
    }
}
