// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Stores progressive-pass coefficient shifts and resolution transitions.
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
    /// Initializes one full-resolution pass with no explicit downsampling transitions.
    /// </summary>
    public JxlPasses() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets the number of passes.
    /// </summary>
    public uint NumberOfPasses
    {
        get => this.numPasses;
        set => this.numPasses = value;
    }

    /// <summary>
    /// Gets or sets the number of declared resolution transitions.
    /// </summary>
    public uint NumberOfDownsamples
    {
        get => this.numDownsample;
        set => this.numDownsample = value;
    }

    /// <summary>
    /// Gets the spatial reduction factor reached at each declared transition.
    /// </summary>
    public uint[] Downsample { get; } = new uint[MaxPasses];

    /// <summary>
    /// Gets the zero-based final pass index for each resolution transition.
    /// </summary>
    public uint[] LastPass { get; } = new uint[MaxPasses];

    /// <summary>
    /// Gets the coefficient bit shift for each pass. The final pass has an implicit zero shift.
    /// </summary>
    public uint[] Shift { get; } = new uint[MaxPasses];

    /// <summary>
    /// Gets the spatial refinement shifts covered by one valid pass index.
    /// </summary>
    /// <param name="pass">The zero-based pass index, below NumberOfPasses.</param>
    /// <param name="minShift">The smallest shift reconstructed by this pass.</param>
    /// <param name="maxShift">The largest shift reconstructed by this pass.</param>
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

    /// <summary>
    /// Gets the downsampling factor after the specified number of completed passes.
    /// </summary>
    /// <param name="completedPassCount">The number of passes already decoded.</param>
    /// <returns>The available factor: eight before refinement, then the declared factors, and one after all passes.</returns>
    public uint GetDownsamplingTargetForCompletedPasses(int completedPassCount)
    {
        if (completedPassCount >= this.numPasses)
        {
            return 1;
        }

        // Before any declared transition, JPEG XL exposes the one-eighth-resolution DC image.
        uint result = 8;

        for (int i = 0; i < this.numDownsample; i++)
        {
            if (completedPassCount > this.LastPass[i])
            {
                result = Math.Min(result, this.Downsample[i]);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        // This distribution bounds the count to 1..11, which also bounds the fixed pass arrays.
        if (!visitor.U32(
            JxlFieldExpressions.Value(1),
            JxlFieldExpressions.Value(2),
            JxlFieldExpressions.Value(3),
            JxlFieldExpressions.BitsOffset(3, 4),
            1,
            ref this.numPasses))
        {
            return false;
        }

        if (visitor.Conditional(this.numPasses != 1))
        {
            // There are at most four transitions because their factors must strictly decrease through 8,4,2,1.
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

            if (this.numDownsample > this.numPasses)
            {
                return false;
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
                    return false;
                }
            }

            for (int i = 0; i < this.numDownsample; i++)
            {
                if (!visitor.U32(
                    JxlFieldExpressions.Value(0),
                    JxlFieldExpressions.Value(1),
                    JxlFieldExpressions.Value(2),
                    JxlFieldExpressions.Bits(3),
                    0,
                    ref this.LastPass[i]))
                {
                    return false;
                }

                if (i > 0 && this.LastPass[i] <= this.LastPass[i - 1])
                {
                    return false;
                }

                if (this.LastPass[i] >= this.numPasses)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
