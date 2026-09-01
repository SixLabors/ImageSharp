// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines smooth interpolation for translational inter prediction.
/// </content>
internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// Selects smooth interpolation coefficients.
    /// </summary>
    internal readonly struct SmoothOperator : IAv1InterPredictorOperator
    {
        /// <inheritdoc/>
        public static ReadOnlySpan<short> GetCoefficients(int phase, bool useReducedFilter)
            => GetPhase(useReducedFilter ? SmoothFourTap : SmoothEightTap, phase);
    }
}
