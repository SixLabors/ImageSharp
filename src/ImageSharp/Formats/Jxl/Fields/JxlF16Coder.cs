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

    public static bool Write(float value, JxlBitWriter writer)
    {
        uint bits32 = BitConverter.SingleToUInt32Bits(value);

        uint sign = bits32 >> 31;
        uint biasedExp32 = (bits32 >> 23) & 0xFF;
        uint mantissa32 = bits32 & 0x7FFFFF;

        int exp = (int)biasedExp32 - 127;
        if (exp > 15)
        {
            throw new InvalidOperationException("Too big to encode, CanEncode should return false");
        }

        // Tiny or zero => zero.
        if (exp < -24)
        {
            writer.Write(16, 0);
            return true;
        }

        uint biasedExp16 = 0;
        uint mantissa16 = 0;

        if (exp < -14)
        {
            biasedExp16 = 0;

            uint subExp = unchecked((uint)(-14 - exp));

            if (subExp is not (>= 1 and < 11))
            {
                return false;
            }

            mantissa16 = (1u << (int)(10 - subExp)) + (mantissa32 >> (int)(13 + subExp));
        }
        else
        {
            // exp = [-14, 15]
            biasedExp16 = unchecked((uint)(exp + 15));

            if (biasedExp16 is not (>= 1 and < 31))
            {
                return false;
            }

            mantissa16 = mantissa32 >> 13;
        }

        if (mantissa16 >= 1024)
        {
            return false;
        }

        uint bits16 = (sign << 15) | (biasedExp16 << 10) | mantissa16;

        if (bits16 >= 0x10000)
        {
            return false;
        }

        writer.Write(16, bits16);
        return true;
    }
}
