// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ProgressiveSplit;

internal struct JxlProgressiveSplitter
{
    private JxlProgressiveMode progressiveMode;

    public void SetProgressiveMode(JxlProgressiveMode mode) => this.progressiveMode = mode;

    public readonly int GetNumPasses() => this.progressiveMode.NumPasses;

    public readonly void InitializePasses(JxlPasses passes)
    {
        passes.NumberOfPasses = (uint)this.GetNumPasses();
        passes.NumberOfDownsamples = 0u;

        if (passes.NumberOfPasses == 0)
        {
            throw new InvalidOperationException("At least one pass must be present");
        }

        passes.Shift[(int)passes.NumberOfPasses - 1] = 0;

        if (passes.NumberOfPasses == 1)
        {
            // Done. Arrays are empty.
            return;
        }

        for (int i = 0; i < this.progressiveMode.NumPasses - 1; ++i)
        {
            int minDownsamplingFactor = this.progressiveMode.Passes[i].SuitableForDownsamplingOfAtLeast;
            passes.Shift[i] = (uint)this.progressiveMode.Passes[i].Shift;

            if (minDownsamplingFactor is > 1 and not int.MaxValue)
            {
                passes.Downsample[(int)passes.NumberOfDownsamples] = (uint)minDownsamplingFactor;
                passes.LastPass[(int)passes.NumberOfDownsamples] = (uint)i;

                if (this.progressiveMode.Passes[i + 1].SuitableForDownsamplingOfAtLeast < minDownsamplingFactor)
                {
                    passes.NumberOfDownsamples++;
                }
            }
        }
    }

    public readonly void SplitAcCoefficients<T>(Span<T> block, JxlAcStrategy acs, int bx, int by, Span<InlineArray11<T>> output)
        where T : unmanaged, INumber<T>, IShiftOperators<T, T, T>
    {
        int size = acs.CoveredBlocksX * acs.CoveredBlocksY * JxlFrameDimensions.DctBlockSize;

        if (this.progressiveMode.NumPasses == 1)
        {
            block.CopyTo(output[0]);
            return;
        }

        int nCoeffsAllDoneFromEarlierPasses = 1;
        int previousPassShift = 0;

        for (int passNumber = 0; passNumber < this.progressiveMode.NumPasses; passNumber++)
        {
            MemoryMarshal.Cast<InlineArray11<T>, T>(output[passNumber..]).Slice(0, size).Clear();

            int passShift = this.progressiveMode.Passes[passNumber].Shift;
            int frameNCoeffs = this.progressiveMode.Passes[passNumber].NumCoefficients;
            int xsize = acs.CoveredBlocksX;
            int ysize = acs.CoveredBlocksY;

            JxlForwardCoefficientOrder.CoefficientLayout(ref ysize, ref xsize);

            for (int y = 0; y < ysize * frameNCoeffs; y++)
            {
                for (int x = 0; x < xsize * frameNCoeffs; x++)
                {
                    int pos = (y * xsize * JxlFrameDimensions.BlockDimensions) + x;

                    if (x < xsize * nCoeffsAllDoneFromEarlierPasses && y < ysize * nCoeffsAllDoneFromEarlierPasses)
                    {
                        // This coefficient was already included in an earlier pass,
                        // which included a genuinely smaller set of coefficients.
                        continue;
                    }

                    T v = block[pos];

                    if (previousPassShift != 0)
                    {
                        T previousV = ShiftRightRound0(v, previousPassShift) * T.CreateSaturating(1 << previousPassShift);
                        v -= previousV;
                    }

                    output[passNumber][pos] = ShiftRightRound0(v, passShift);
                }
            }

            if (this.progressiveMode.Passes[passNumber].Shift == 0)
            {
                nCoeffsAllDoneFromEarlierPasses = frameNCoeffs;
            }

            previousPassShift = this.progressiveMode.Passes[passNumber].Shift;
        }

        static T ShiftRightRound0(T v, int shift)
        {
            T oneIfNegative = v >> T.CreateSaturating(31);
            T add = (oneIfNegative << T.CreateSaturating(shift)) - oneIfNegative;
            return (v + add) >> T.CreateSaturating(shift);
        }
    }
}
