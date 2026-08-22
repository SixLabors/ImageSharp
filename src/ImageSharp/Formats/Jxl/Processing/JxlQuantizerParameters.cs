// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Represents parameters for the JPEG XL quantizer.
/// </summary>
internal sealed class JxlQuantizerParameters : IJxlFields
{
    private uint globalScale;
    private uint quantDc;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlQuantizerParameters"/> class.
    /// </summary>
    public JxlQuantizerParameters() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets the global scale value.
    /// </summary>
    public uint GlobalScale
    {
        get => this.globalScale;
        set => this.globalScale = value;
    }

    /// <summary>
    /// Gets or sets the quant DC value.
    /// </summary>
    public uint QuantDc
    {
        get => this.quantDc;
        set => this.quantDc = value;
    }

    public bool Visit(JxlVisitor visitor)
    {
        if (!visitor.U32(
            JxlFieldExpressions.BitsOffset(11, 1),
            JxlFieldExpressions.BitsOffset(11, 2049),
            JxlFieldExpressions.BitsOffset(12, 4097),
            JxlFieldExpressions.BitsOffset(16, 8193),
            1,
            ref this.globalScale))
        {
            return false;
        }

        return visitor.U32(
            JxlFieldExpressions.Value(16),
            JxlFieldExpressions.BitsOffset(5, 1),
            JxlFieldExpressions.BitsOffset(8, 1),
            JxlFieldExpressions.BitsOffset(16, 1),
            1,
            ref this.quantDc);
    }
}
