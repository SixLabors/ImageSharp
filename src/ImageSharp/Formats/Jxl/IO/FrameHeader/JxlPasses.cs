// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Used for decoding to lower resolutions.
/// </summary>
internal sealed class JxlPasses : IJxlFields
{
    /// <summary>
    /// Defines the maximum amount of passes, which is 11.
    /// </summary>
    private const int MaxPasses = 11;

    private uint numPasses;
    private uint numDownsample;

    /// <summary>
    /// Gets or sets the number of passes.
    /// </summary>
    public uint NumberOfPasses
    {
        get => this.numPasses;
        set => this.numPasses = value;
    }

    /// <summary>
    /// Gets or sets the number of downsamples.
    /// </summary>
    public uint NumberOfDownsamples
    {
        get => this.numDownsample;
        set => this.numDownsample = value;
    }

    /// <summary>
    /// Gets the downsample values.
    /// </summary>
    public uint[] Downsample { get; } = new uint[MaxPasses];

    /// <summary>
    /// Gets the last pass values.
    /// </summary>
    public uint[] LastPass { get; } = new uint[MaxPasses];

    /// <summary>
    /// Gets the shift values.
    /// </summary>
    public uint[] Shift { get; } = new uint[MaxPasses];

    public void GetDownsamplingBracket(int pass, out int minShift, out int maxShift)
    {
        maxShift = 2;
        minShift = 3;

        for (int i = 0; ; i++)
        {
            for (int j = 0; j < this.numDownsample; ++j)
            {
                if (i == this.LastPass[j])
                {
                    uint ds = this.Downsample[j];

                    if (ds == 8)
                    {
                        minShift = 3;
                    }

                    if (ds == 4)
                    {
                        minShift = 2;
                    }

                    if (ds == 2)
                    {
                        minShift = 1;
                    }

                    if (ds == 1)
                    {
                        minShift = 0;
                    }
                }
            }

            if (i == this.numPasses - 1)
            {
                minShift = 0;
            }

            if (i == pass)
            {
                return;
            }

            maxShift = minShift - 1;
        }
    }

    public uint GetDownsamplingTargetForCompletedPasses(int num)
    {
        if (num >= this.numPasses)
        {
            return 1;
        }

        uint result = 0;

        for (int i = 0; i < this.numDownsample; i++)
        {
            if (num > this.LastPass[i])
            {
                result = Math.Min(result, this.Downsample[i]);
            }
        }

        return result;
    }

    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.U32(
            JxlFieldExpressions.Value(1),
            JxlFieldExpressions.Value(2),
            JxlFieldExpressions.Value(2),
            JxlFieldExpressions.BitsOffset(1, 3),
            0,
            ref this.numPasses))
        {
            return false;
        }

        if (this.numPasses > MaxPasses)
        {
            return false;
        }

        if (visitor.Conditional(this.numPasses != 1))
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(2),
                JxlFieldExpressions.BitsOffset(1, 3),
                0,
                ref this.numDownsample))
            {
                return false;
            }

            if (this.numDownsample > 4)
            {
                return false;
            }

            if (this.numDownsample > this.numPasses)
            {
                throw new InvalidOperationException("Number of downsaples is greater than number of passes");
            }

            for (int i = 0; i < this.numPasses - 1; i++)
            {
                if (!visitor.Bits(2, 0u, ref this.Shift[i]))
                {
                    return false;
                }
            }

            this.Shift[this.numPasses - 1] = 0;

            for (int i = 0; i < this.numDownsample; i++)
            {
                if (!visitor.U32(
                    JxlFieldExpressions.Value(1),
                    JxlFieldExpressions.Value(2),
                    JxlFieldExpressions.Value(4),
                    JxlFieldExpressions.Value(8),
                    1,
                    ref this.Downsample[i]))
                {
                    return false;
                }

                if (i > 0 && this.Downsample[i] >= this.Downsample[i - 1])
                {
                    throw new InvalidOperationException("Downsample sequence should decrease");
                }
            }

            for (int i = 0; i < this.numDownsample; i++)
            {
                if (!visitor.U32(
                    JxlFieldExpressions.Value(0),
                    JxlFieldExpressions.Value(1),
                    JxlFieldExpressions.Value(2),
                    JxlFieldExpressions.Value(3),
                    0,
                    ref this.LastPass[i]))
                {
                    return false;
                }

                if (i > 0 && this.LastPass[i] <= this.LastPass[i - 1])
                {
                    throw new InvalidOperationException("Last pass sequence should increase");
                }

                if (this.LastPass[i] >= this.numPasses)
                {
                    throw new InvalidOperationException("Last pass is greater than number of passes");
                }
            }
        }

        return true;
    }
}
