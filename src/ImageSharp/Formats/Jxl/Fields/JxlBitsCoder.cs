// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Raw bits coder
/// </summary>
internal static class JxlBitsCoder
{
    /// <summary>
    /// Maximum number of encodeable bits. Since this coder encodes
    /// bits raw, this happens to be whatever is passed to it.
    /// </summary>
    // Looks like that's what the function does (fields.cc:406):
    // it returns whatever is passed to it.
    public static int MaxEncodedBits(int bits) => bits;

    public static bool CanEncode(int bits, uint value, ref int encodedBits)
    {
        encodedBits = bits;
        if (value >= (1 << bits))
        {
            DebugGuard.IsTrue(false, "Value is too large");

            return false;
        }

        return true;
    }

    // NOTE: BitsCoder::Read (fields.cc:418) returns a uint32_t,
    // suggesting the input bit size does not exceed 32 bits.
    public static uint Read(uint bits, JxlBitReader reader) => reader.ReadBits32(bits);

    public static uint Read(int bits, JxlBitReader reader) => reader.ReadBits32((uint)bits);
}
