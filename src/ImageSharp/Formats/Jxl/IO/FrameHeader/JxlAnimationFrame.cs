// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Metadata;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Describes duration of frames that make up an animation.
/// </summary>
internal sealed class JxlAnimationFrame : IJxlFields
{
    /// <summary>
    /// See <see cref="Duration"/>.
    /// </summary>
    private uint duration;

    /// <summary>
    /// See <see cref="Timecode"/>.
    /// </summary>
    private uint timecode;

    /// <summary>
    /// Gets or sets the duration of the animation.
    /// </summary>
    public uint Duration
    {
        get => this.duration;
        set => this.duration = value;
    }

    /// <summary>
    /// Gets or sets the timecode of the animation. The
    /// format is 0xHHMMSSFF.
    /// </summary>
    public uint Timecode
    {
        get => this.timecode;
        set => this.timecode = value;
    }

    /// <summary>
    /// Gets or sets the optional codec metadata.
    /// </summary>
    public JxlCodecMetadata? CodecMetadata { get; set; }

    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.Conditional(this.CodecMetadata?.ImageMetadata?.HaveAnimation == true))
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Bits(8),
                JxlFieldExpressions.Bits(32),
                0,
                ref this.duration))
            {
                return false;
            }
        }

        if (visitor.Conditional(this.CodecMetadata?.ImageMetadata?.Animation?.ContainsTimecodes == true))
        {
            if (!visitor.Bits(32, 0u, ref this.timecode))
            {
                return false;
            }
        }

        return true;
    }
}
