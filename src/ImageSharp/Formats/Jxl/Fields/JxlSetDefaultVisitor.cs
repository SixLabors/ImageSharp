// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Restores serialized defaults in a bundle and its nested bundles.
/// </summary>
internal sealed class JxlSetDefaultVisitor : JxlVisitor
{
    public override bool Bits(int bits, uint defaultValue, ref uint value)
    {
        value = defaultValue;
        return true;
    }

    /// <inheritdoc />
    public override bool U32(JxlU32Enc enc, uint defaultValue, ref uint value)
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
