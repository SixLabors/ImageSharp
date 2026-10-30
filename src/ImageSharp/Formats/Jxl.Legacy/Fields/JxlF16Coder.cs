// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Reads and writes JPEG XL half-precision fields.
/// </summary>
internal static class JxlF16Coder
{
    /// <summary>
    /// Always returns 16, which is the maximum possible encoded bits.
    /// The F16 coder always reads 16 bits from the bitstream.
    /// </summary>
    public static int MaxEncodedBits() => 16;

    /// <summary>
    /// Checks whether a value is finite and within the half-precision range.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="encodedBits">The field width, set to 16 even when the value is invalid.</param>
    /// <returns>Whether the value can be rounded to a finite half without saturation.</returns>
    public static bool CanEncode(float value, ref int encodedBits)
    {
        encodedBits = MaxEncodedBits();

        // Use the shared finite-range rules. A changed value is outside the wire field's accepted range;
        // NaN also fails equality. In-range values, including either signed zero, remain valid.
        return value == ColorNumerics.ClampToFinite(value, (float)Half.MinValue, (float)Half.MaxValue);
    }

    /// <summary>
    /// Reads one half-precision field, rejecting the NaN and infinity encodings forbidden by JPEG XL.
    /// </summary>
    /// <param name="reader">The reader positioned at the 16-bit field.</param>
    /// <param name="value">The decoded value, unchanged when its encoding is invalid.</param>
    /// <returns>Whether the field contains a finite value.</returns>
    public static bool Read(JxlBitReader reader, ref float value)
    {
        ushort bits = (ushort)reader.ReadBits32(16u);

        // The wire format forbids an all-ones exponent. All other bit patterns, including signed zero
        // and subnormal values, use the existing half-storage conversion without color-range clamping.
        if ((bits & 0x7C00) == 0x7C00)
        {
            return false;
        }

        value = HalfTypeHelper.Unpack(bits);
        return true;
    }

    /// <summary>
    /// Rounds a finite value to the nearest half-precision value and writes its 16-bit field.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <returns>Whether the value is finite and within the half-precision range. Invalid values write no bits.</returns>
    /// <remarks>Halfway values round to the value with an even low mantissa bit. Signed zero is preserved.</remarks>
    public static bool Write(float value, JxlBitWriter writer)
    {
        int encodedBits = 0;
        if (!CanEncode(value, ref encodedBits))
        {
            return false;
        }

        // Shared half packing supplies nearest rounding, including subnormal boundaries and signed zero.
        // The range check excludes values that can produce a nonfinite wire encoding.
        writer.Write(encodedBits, HalfTypeHelper.Pack(value));
        return true;
    }
}
