// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ProgressiveSplit;

internal struct JxlProgressiveMode
{
    public int NumPasses = 1;
    public InlineArray11<JxlPassDefinition> Passes;

    public JxlProgressiveMode()
    {
        Span<JxlPassDefinition> passesSpan = this.Passes;
        JxlPassDefinition definitionToFill = new(numCoefficients: 8, shift: 0, suitableForDownsamplingOfAtLeast: 1);
        passesSpan.Fill(definitionToFill);
    }

    public JxlProgressiveMode(Span<JxlPassDefinition> passes)
    {
        int nump = passes.Length;
        DebugGuard.MustBeLessThanOrEqualTo(nump, JxlShared.MaximumNumberOfPasses, nameof(nump));

        this.NumPasses = nump;
        JxlPassDefinition previousPass = new(1, 0, int.MaxValue);
        int lastDownsamplingFactor = int.MaxValue;

        for (int i = 0; i < nump; i++)
        {
            ref JxlPassDefinition p = ref passes[i];

            if (!(p.NumCoefficients > previousPass.NumCoefficients ||
                  (p.NumCoefficients == previousPass.NumCoefficients &&
                   p.Shift < previousPass.Shift)))
            {
                throw new InvalidOperationException("The pass is invalid");
            }

            if (!(p.SuitableForDownsamplingOfAtLeast == int.MaxValue ||
                  p.SuitableForDownsamplingOfAtLeast <= lastDownsamplingFactor))
            {
                throw new InvalidOperationException("The pass is invalid");
            }

            if (p.SuitableForDownsamplingOfAtLeast != int.MaxValue)
            {
                lastDownsamplingFactor = p.SuitableForDownsamplingOfAtLeast;
            }

            previousPass = passes[i] = p;
        }
    }
}
