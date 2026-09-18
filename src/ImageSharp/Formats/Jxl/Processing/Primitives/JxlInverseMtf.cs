// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Inverse Move to Front implementation
/// </summary>
internal static class JxlInverseMtf
{
    public static void MoveToFront(Span<byte> values, byte index)
    {
        byte value = values[index];

        // CopyTo supports overlapping source and destination regions.
        values[..index].CopyTo(values[1..]);
        values[0] = value;
    }

    public static void InverseMoveToFrontTransform(Span<byte> values)
    {
        Span<byte> table = stackalloc byte[256];

        for (int i = 0; i < table.Length; i++)
        {
            table[i] = (byte)i;
        }

        for (int i = 0; i < values.Length; i++)
        {
            byte index = values[i];
            byte value = table[index];
            values[i] = value;

            if (index != 0)
            {
                // CopyTo handles the overlap and shifts the preceding entries.
                table[..index].CopyTo(table[1..]);
                table[0] = value;
            }
        }
    }
}
