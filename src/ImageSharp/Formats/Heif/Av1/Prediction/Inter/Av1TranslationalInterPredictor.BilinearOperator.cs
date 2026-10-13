// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines bilinear interpolation for translational inter prediction.
/// </content>
internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// Selects bilinear interpolation coefficients.
    /// </summary>
    internal readonly struct BilinearOperator : IAv1InterPredictorOperator
    {
        /// <inheritdoc/>
        public static ReadOnlySpan<short> GetCoefficients(int phase, bool useReducedFilter)
            => GetPhase(Bilinear, phase);
    }
}
