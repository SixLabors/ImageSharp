// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Represents the Inverse Move-to-Front (MTF) Transform.
/// </summary>
internal static class JxlInverseMtf
{
    /// <summary>
    /// Moves bytes to the front starting at index <paramref name="index"/>.
    /// </summary>
    /// <param name="values">The MTF bytes.</param>
    /// <param name="index">The MTF index.</param>
    public static void MoveToFront(Span<byte> values, byte index)
    {
        byte value = values[index];

        // CopyTo supports overlapping source and destination regions.
        values[..index].CopyTo(values[1..]);
        values[0] = value;
    }

    /// <summary>
    /// Performs the Inverse Move-to-Front Transform on the span of bytes.
    /// </summary>
    /// <param name="values">The MTF bytes.</param>
    public static void InverseMoveToFrontTransform(Span<byte> values)
    {
        Span<byte> table = stackalloc byte[256];

        // First, we populate the table with indices.
        // TODO: JxlSimdUtils.Iota<byte>?
        for (int i = 0; i < table.Length; i++)
        {
            table[i] = (byte)i;
        }

        // Then we do the actual inverse transform here.
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
