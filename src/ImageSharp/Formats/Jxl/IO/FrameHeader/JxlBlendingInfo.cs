// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Provides options and instructions that tell the decoder the proper
/// way to blend the current and previous frame together.
/// </summary>
internal sealed class JxlBlendingInfo : IJxlFields
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBlendingInfo"/> class.
    /// </summary>
    public JxlBlendingInfo() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets the blending mode. See <see cref="JxlBlendMode"/>.
    /// </summary>
    public JxlBlendMode BlendMode { get; set; }

    /// <summary>
    /// Gets or sets the value that indicates which extra channel
    /// to use as alpha channel for blending.
    /// </summary>
    public uint AlphaChannel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the alpha or channel values
    /// must be clamped* to the 0 through 1 range.
    /// </summary>
    /// <remarks>
    /// Clamped - must be limited to the specified range.
    /// </remarks>
    public bool Clamp { get; set; }

    /// <summary>
    /// Gets or sets the frame ID to copy from (0 through 3).
    /// </summary>
    /// <remarks>
    /// If <see cref="BlendMode"/> is equal to <see cref="JxlBlendMode.Replace"/>,
    /// the value of this property is ignored.
    /// </remarks>
    public uint Source { get; set; }

    /// <summary>
    /// Gets or sets the total number of extra channels.
    /// </summary>
    public int ExtraChannelCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame is partial.
    /// </summary>
    public bool IsPartialFrame { get; set; }

    public bool Visit(JxlVisitor visitor)
    {
        JxlBlendMode mode = this.BlendMode;
        if (!VisitBlendMode(visitor, JxlBlendMode.Replace, ref mode))
        {
            return false;
        }

        this.BlendMode = mode;

        if (visitor.Conditional(this.ExtraChannelCount > 0 && mode is JxlBlendMode.Blend or JxlBlendMode.AlphaWeightedBlend))
        {
            uint alphaChannel = this.AlphaChannel;
            if (!visitor.U32(
                JxlFieldExpressions.Value(0u),
                JxlFieldExpressions.Value(1u),
                JxlFieldExpressions.Value(2u),
                JxlFieldExpressions.BitsOffset(3u, 3u),
                0,
                ref alphaChannel))
            {
                return false;
            }

            this.AlphaChannel = alphaChannel;

            if (visitor.IsReading && alphaChannel >= this.ExtraChannelCount)
            {
                throw new InvalidOperationException("Invalid alpha channel for blending");
            }
        }

        if (visitor.Conditional((this.ExtraChannelCount > 0 && mode is JxlBlendMode.Blend or JxlBlendMode.AlphaWeightedBlend) || mode == JxlBlendMode.Multiply))
        {
            bool clamp = this.Clamp;

            if (!visitor.Boolean(false, ref clamp))
            {
                return false;
            }

            this.Clamp = clamp;
        }

        if (visitor.Conditional(mode != JxlBlendMode.Replace || this.IsPartialFrame))
        {
            uint source = this.Source;

            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.Value(3),
                0,
                ref source))
            {
                return false;
            }

            this.Source = source;
        }

        return true;
    }

    private static bool VisitBlendMode(JxlVisitor visitor, JxlBlendMode defaultValue, ref JxlBlendMode valueToEncode)
    {
        uint unsignedBackingValue = (uint)valueToEncode;

        if (!visitor.U32(
            JxlFieldExpressions.Value((uint)JxlBlendMode.Replace),
            JxlFieldExpressions.Value((uint)JxlBlendMode.Add),
            JxlFieldExpressions.Value((uint)JxlBlendMode.Blend),
            JxlFieldExpressions.BitsOffset(2u, 3u),
            (uint)defaultValue,
            ref unsignedBackingValue))
        {
            return false;
        }

        if (unsignedBackingValue > (uint)JxlBlendMode.Multiply)
        {
            throw new InvalidOperationException("Invalid blend mode");
        }

        valueToEncode = (JxlBlendMode)unsignedBackingValue;
        return true;
    }
}
