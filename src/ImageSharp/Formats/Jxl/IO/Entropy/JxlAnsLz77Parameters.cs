// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

internal sealed class JxlAnsLz77Parameters : IJxlFields
{
    private bool enabled;
    private uint minimumSymbol;
    private uint minimumLength;
    private JxlAnsHybridUIntConfiguration lengthUintConfig = new(0, 0, 0);

    public JxlAnsLz77Parameters() => JxlBundle.Init(this);

    public bool Enabled
    {
        get => this.enabled;
        set => this.enabled = value;
    }

    public uint MinimumSymbol
    {
        get => this.minimumSymbol;
        set => this.minimumSymbol = value;
    }

    public uint MinimumLength
    {
        get => this.minimumLength;
        set => this.minimumLength = value;
    }

    public JxlAnsHybridUIntConfiguration LengthUintConfig
    {
        get => this.lengthUintConfig;
        set => this.lengthUintConfig = value;
    }

    public int NonserializedDistanceContext { get; set; }

    public ref JxlAnsHybridUIntConfiguration GetLengthUIntConfigReference() => ref this.lengthUintConfig;

    public bool Visit(JxlVisitor visitor)
    {
        if (!visitor.Boolean(false, ref this.enabled))
        {
            return false;
        }

        if (!visitor.Conditional(this.enabled))
        {
            return true;
        }

        if (!visitor.U32(
            JxlFieldExpressions.Value(224u),
            JxlFieldExpressions.Value(512u),
            JxlFieldExpressions.Value(4096u),
            JxlFieldExpressions.BitsOffset(15u, 8u),
            224u,
            ref this.minimumSymbol))
        {
            return false;
        }

        if (!visitor.U32(
            JxlFieldExpressions.Value(3u),
            JxlFieldExpressions.Value(4u),
            JxlFieldExpressions.BitsOffset(2u, 5u),
            JxlFieldExpressions.BitsOffset(8u, 9u),
            3u,
            ref this.minimumLength))
        {
            return false;
        }

        return true;
    }
}
