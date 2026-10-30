// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

/// <summary>
/// Stores the JPEG XL animation time base, repetition count and timecode flag.
/// </summary>
internal sealed class JxlAnimationHeader : IJxlFields
{
    private uint tpsNumerator;
    private uint tpsDenominator;
    private uint loopCount;
    private bool containsTimecodes;

    /// <summary>
    /// Initializes an animation header with one tick per second and unlimited repetition.
    /// </summary>
    public JxlAnimationHeader() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets the numerator of the animation ticks-per-second ratio.
    /// </summary>
    public uint TpsNumerator
    {
        get => this.tpsNumerator;
        set => this.tpsNumerator = value;
    }

    /// <summary>
    /// Gets or sets the denominator of the animation ticks-per-second ratio.
    /// </summary>
    public uint TpsDenominator
    {
        get => this.tpsDenominator;
        set => this.tpsDenominator = value;
    }

    /// <summary>
    /// Gets or sets the number of times to play the animation. Zero means unlimited playback.
    /// </summary>
    public uint LoopCount
    {
        get => this.loopCount;
        set => this.loopCount = value;
    }

    /// <summary>
    /// Gets or sets whether frame headers include timecodes.
    /// </summary>
    public bool ContainsTimecodes
    {
        get => this.containsTimecodes;
        set => this.containsTimecodes = value;
    }

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        // The positive numerator and denominator form ticks per second. Common rates have direct two-bit alternatives.
        if (!visitor.U32(
            JxlFieldExpressions.Value(100),
            JxlFieldExpressions.Value(1000),
            JxlFieldExpressions.BitsOffset(10, 1),
            JxlFieldExpressions.BitsOffset(30, 1),
            1,
            ref this.tpsNumerator))
        {
            return false;
        }

        if (!visitor.U32(
            JxlFieldExpressions.Value(1),
            JxlFieldExpressions.Value(1001),
            JxlFieldExpressions.BitsOffset(8, 1),
            JxlFieldExpressions.BitsOffset(10, 1),
            1,
            ref this.tpsDenominator))
        {
            return false;
        }

        // Loop count has a full UInt32 alternative. Keep it unsigned instead of narrowing the high-bit values to Int32.
        if (!visitor.U32(
            JxlFieldExpressions.Value(0),
            JxlFieldExpressions.Bits(3),
            JxlFieldExpressions.Bits(16),
            JxlFieldExpressions.Bits(32),
            0,
            ref this.loopCount))
        {
            return false;
        }

        return visitor.Boolean(false, ref this.containsTimecodes);
    }
}
