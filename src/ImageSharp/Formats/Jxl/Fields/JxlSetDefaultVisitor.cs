// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// This is similar to InitVisitor but also initializes
/// nested fields.
/// </summary>
internal sealed class JxlSetDefaultVisitor : JxlVisitorBase
{
    public override bool Bits(int bits, uint defaultValue, ref uint value)
    {
        value = defaultValue;
        return true;
    }

    public override bool U32(JxlU32Distribution d0, JxlU32Distribution d1, JxlU32Distribution d2, JxlU32Distribution d3, uint defaultValue, ref uint value)
    {
        value = defaultValue;
        return true;
    }

    public override bool U64(ulong defaultValue, ref ulong value)
    {
        value = defaultValue;
        return true;
    }

    public override bool Boolean(bool defaultValue, ref bool value)
    {
        value = defaultValue;
        return true;
    }

    public override bool F16(float defaultValue, ref float value)
    {
        value = defaultValue;
        return true;
    }

    public override bool Conditional(bool condition) => true;

    public override bool AllDefault(IJxlFields fields, ref bool allDefault)
    {
        _ = this.Boolean(true, ref allDefault);
        return false;
    }
}
