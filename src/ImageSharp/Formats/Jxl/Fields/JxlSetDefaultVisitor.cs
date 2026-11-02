// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Sets serialized fields to their defaults, with explicit control of nested-bundle traversal.
/// </summary>
/// <param name="visitNested">Whether to reset existing nested bundles as well as the current bundle's own fields.</param>
internal sealed class JxlSetDefaultVisitor(bool visitNested) : JxlVisitor
{
    /// <inheritdoc />
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

    /// <inheritdoc />
    public override bool U64(ulong defaultValue, ref ulong value)
    {
        value = defaultValue;
        return true;
    }

    /// <inheritdoc />
    public override bool Boolean(bool defaultValue, ref bool value)
    {
        value = defaultValue;
        return true;
    }

    /// <inheritdoc />
    public override bool F16(float defaultValue, ref float value)
    {
        value = defaultValue;
        return true;
    }

    /// <inheritdoc />
    public override bool Conditional(bool condition) => true;

    /// <inheritdoc />
    public override bool VisitNested(IJxlFields fields)
    {
        // Initialization touches only the parent's fields: each nested bundle has its own constructor.
        // An explicit reset instead visits existing children in their normal extension scopes.
        return !visitNested || base.VisitNested(fields);
    }

    /// <inheritdoc />
    public override bool AllDefault(IJxlFields fields, ref bool allDefault)
    {
        _ = this.Boolean(true, ref allDefault);
        return false;
    }
}
