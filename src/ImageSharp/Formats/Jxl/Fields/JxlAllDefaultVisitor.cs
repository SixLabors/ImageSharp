// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

internal sealed class JxlAllDefaultVisitor : JxlVisitorBase
{
    public bool IsAllDefault { get; private set; } = true;

    public override bool Bits(int bits, uint defaultValue, ref uint value)
    {
        this.IsAllDefault = value == defaultValue;
        return true;
    }

    public override bool U32(JxlU32Enc enc, uint defaultValue, ref uint value)
    {
        this.IsAllDefault = value == defaultValue;
        return true;
    }

    public override bool U64(ulong defaultValue, ref ulong value)
    {
        this.IsAllDefault = value == defaultValue;
        return true;
    }

    public override bool F16(float defaultValue, ref float value)
    {
        this.IsAllDefault = MathF.Abs(value - defaultValue) < 1E-6f;
        return true;
    }

    public override bool AllDefault(IJxlFields fields, ref bool allDefault) => false;
}
