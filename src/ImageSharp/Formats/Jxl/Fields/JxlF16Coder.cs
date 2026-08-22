// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Represents the Half-precision Floating-point number coder.
/// </summary>
internal static class JxlF16Coder
{
    /// <summary>
    /// Always returns 16, which is the maximum possible encoded bits.
    /// The F16 coder always reads 16 bits from the bitstream.
    /// </summary>
    public static int MaxEncodedBits() => 16;

    /// <summary>
    /// Returns a boolean indicating whether the input float
    /// can be represented properly when encoded into a bit-stream.
    /// Also stores the maximum encodeable bits into encodedBits (which is
    /// always 16).
    /// </summary>
    public static bool CanEncode(float value, ref int encodedBits)
    {
        encodedBits = MaxEncodedBits();
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return false; // NaN and Infinity are not valid
        }

        return MathF.Abs(value) <= 65504.0f;
    }

    public static bool Read(JxlBitReader reader, ref float value)
    {
        uint bits16 = reader.ReadBits32(16u);
        uint sign = bits16 >> 15;
        uint biasedExponent = (bits16 >> 10) & 0x1Fu;
        uint mantissa = bits16 & 0x3FFu;

        if (biasedExponent == 31u)
        {
            // NaN and Infinity are not valid
            return false;
        }

        if (biasedExponent == 0u)
        {
            // Subnormal or zero.
            value = (1.0f / 16384) * (mantissa * (1.0f / 1024));
            if (sign != 0u)
            {
                value = -value;
            }

            return true;
        }

        uint biasedExp32 = biasedExponent + (127u - 15u);
        uint mantissa32 = mantissa << (23 - 10);
        uint bits32 = (sign << 31) | (biasedExp32 << 23) | mantissa32;

        value = BitConverter.UInt32BitsToSingle(bits32);

        return true;
    }
}
