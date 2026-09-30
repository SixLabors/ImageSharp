// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Noise;

internal sealed class JxlLossFunction(ReadOnlyMemory<JxlNoiseLevel> noiseLevels)
{
    public double Compute(Span<double> w, Span<double> df, bool skipRegularization = false)
    {
        const double reg = 0.005;
        const double asym = 1.1;

        double lossFunction = 0;

        w.Clear();

        ReadOnlySpan<JxlNoiseLevel> levels = noiseLevels.Span;

        for (int i = 0; i < levels.Length; i++)
        {
            JxlNoiseLevel nl = levels[i];

            JxlNoiseIndexAndFraction pos = JxlNoiseHelper.IndexAndFraction(nl.Intensity);

            double low = w[pos.Index];
            double hi = w[pos.Index + 1];
            double val = (low * (1.0f - pos.Fraction)) + (hi * pos.Fraction);
            double dist = val - nl.NoiseLevel;

            if (dist > 0)
            {
                lossFunction += asym * dist * dist;
                df[pos.Index] -= asym * (1.0f - pos.Fraction) * dist;
                df[pos.Index + 1] -= asym * pos.Fraction * dist;
            }
            else
            {
                lossFunction += dist * dist;
                df[pos.Index] -= (1.0f - pos.Fraction) * dist;
                df[pos.Index + 1] -= pos.Fraction * dist;
            }
        }

        if (skipRegularization)
        {
            return lossFunction;
        }

        int levelsSize = levels.Length;

        for (int i = 0; i + 1 < w.Length; i++)
        {
            double diff = w[i] - w[i + 1];
            lossFunction += reg * levelsSize * diff * diff;
            df[i] -= reg * diff * levelsSize;
            df[i + 1] += reg * diff * levelsSize;
        }

        return lossFunction;
    }
}
