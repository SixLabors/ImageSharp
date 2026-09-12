// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

internal static class JxlFieldExpressions
{
    public static JxlU32Distribution Value(uint value)
    {
        const uint directConstant = JxlU32Distribution.DirectConstant;

        return new(value | directConstant);
    }

    public static JxlU32Distribution BitsOffset(uint bits, uint offset)
        => new(((bits - 1u) & 0x1Fu) + ((offset & 0x3FFFFFFu) << 5));

    public static JxlU32Distribution Bits(uint value) => BitsOffset(value, 0u);

    public static int MakeBit(int index) => 1 << index;
}
