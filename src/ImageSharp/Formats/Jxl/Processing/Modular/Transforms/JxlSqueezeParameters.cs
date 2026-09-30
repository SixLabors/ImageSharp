// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

/// <summary>
/// Parameters for the squeeze transform.
/// </summary>
internal struct JxlSqueezeParameters : IJxlFields
{
    private bool horizontal;
    private bool inPlace;
    private int beginC;
    private int numC;

    public JxlSqueezeParameters() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets a value indicating whether the transform is horizontal.
    /// </summary>
    public bool Horizontal
    {
        readonly get => this.horizontal;
        set => this.horizontal = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the transform is in-place.
    /// </summary>
    public bool InPlace
    {
        readonly get => this.inPlace;
        set => this.inPlace = value;
    }

    public int BeginC
    {
        readonly get => this.beginC;
        set => this.beginC = value;
    }

    public int NumC
    {
        readonly get => this.numC;
        set => this.numC = value;
    }

    public bool Visit(JxlVisitor visitor)
    {
        if (!visitor.Boolean(false, ref this.horizontal) ||
            !visitor.Boolean(false, ref this.inPlace) ||
            !visitor.U32(
                JxlFieldExpressions.Bits(3),
                JxlFieldExpressions.BitsOffset(6, 8),
                JxlFieldExpressions.BitsOffset(10, 72),
                JxlFieldExpressions.BitsOffset(13, 1096),
                0,
                ref this.beginC) ||
            !visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.Value(3),
                JxlFieldExpressions.BitsOffset(4, 4),
                2,
                ref this.numC))
        {
            return false;
        }

        return true;
    }
}
