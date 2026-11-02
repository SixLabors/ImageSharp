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

    /// <summary>
    /// Initializes one vertical, non-in-place squeeze step starting at channel zero and covering two channels.
    /// </summary>
    public JxlSqueezeParameters()
    {
        // Calling Bundle.Init through IJxlFields would initialize a boxed copy of this value, not the stored step.
        this.horizontal = false;
        this.inPlace = false;
        this.beginC = 0;
        this.numC = 2;
    }

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

    /// <summary>
    /// Gets or sets the first selected channel index.
    /// </summary>
    public int BeginC
    {
        readonly get => this.beginC;
        set => this.beginC = value;
    }

    /// <summary>
    /// Gets or sets the number of selected channels.
    /// </summary>
    public int NumC
    {
        readonly get => this.numC;
        set => this.numC = value;
    }

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        uint beginC = (uint)this.beginC;
        uint numC = (uint)this.numC;

        if (!visitor.Boolean(false, ref this.horizontal) ||
            !visitor.Boolean(false, ref this.inPlace) ||
            !visitor.U32(
                JxlFieldExpressions.Bits(3),
                JxlFieldExpressions.BitsOffset(6, 8),
                JxlFieldExpressions.BitsOffset(10, 72),
                JxlFieldExpressions.BitsOffset(13, 1096),
                0,
                ref beginC) ||
            !visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.Value(3),
                JxlFieldExpressions.BitsOffset(4, 4),
                2,
                ref numC))
        {
            return false;
        }

        // Both wire distributions fit in Int32. Publish the decoded values into the actual value-type fields.
        this.beginC = (int)beginC;
        this.numC = (int)numC;
        return true;
    }
}
