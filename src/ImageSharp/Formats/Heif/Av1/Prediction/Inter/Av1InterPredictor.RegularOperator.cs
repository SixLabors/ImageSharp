// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines regular interpolation for translational inter prediction.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// Selects regular interpolation coefficients.
    /// </summary>
    internal readonly struct RegularOperator : IAv1InterPredictorOperator
    {
        /// <inheritdoc/>
        public static ReadOnlySpan<short> GetCoefficients(int phase, bool useReducedFilter)
            => GetPhase(useReducedFilter ? RegularFourTap : RegularEightTap, phase);
    }
}
