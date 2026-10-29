// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Reads and writes fixed-width unsigned fields.
/// </summary>
internal static class JxlBitsCoder
{
    /// <summary>
    /// Returns the wire length of a fixed-width field.
    /// </summary>
    /// <param name="bits">The field width, from zero through 32.</param>
    /// <returns>The unchanged field width.</returns>
    public static int MaxEncodedBits(int bits) => bits;

    /// <summary>
    /// Checks whether an unsigned value fits the supplied field width.
    /// </summary>
    /// <param name="bits">The field width, from zero through 32.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="encodedBits">The field width, including when the value cannot be encoded.</param>
    /// <returns>Whether the value fits the field.</returns>
    public static bool CanEncode(int bits, uint value, ref int encodedBits)
    {
        encodedBits = bits;

        // A UInt32 shift by 32 wraps its count to zero. Use UInt64 for the exclusive endpoint so a full-width field accepts every UInt32.
        return value < (1UL << bits);
    }

    /// <summary>
    /// Writes a fixed-width field when the value fits without truncation.
    /// </summary>
    /// <param name="bits">The field width, from zero through 32.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <returns>Whether the value fits the field. An invalid value writes no bits.</returns>
    public static bool Write(int bits, uint value, JxlBitWriter writer)
    {
        int encodedBits = 0;
        if (!CanEncode(bits, value, ref encodedBits))
        {
            return false;
        }

        writer.Write(bits, value);
        return true;
    }

    /// <summary>
    /// Reads a fixed-width unsigned field.
    /// </summary>
    /// <param name="bits">The field width, from zero through 32.</param>
    /// <param name="reader">The input bit reader.</param>
    /// <returns>The unsigned field value.</returns>
    public static uint Read(uint bits, JxlBitReader reader) => reader.ReadBits32(bits);

    /// <inheritdoc cref="Read(uint, JxlBitReader)" />
    public static uint Read(int bits, JxlBitReader reader) => reader.ReadBits32((uint)bits);
}
