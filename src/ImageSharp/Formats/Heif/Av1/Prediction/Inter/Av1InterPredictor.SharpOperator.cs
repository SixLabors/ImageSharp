// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines sharp interpolation for translational inter prediction.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// Selects sharp interpolation coefficients.
    /// </summary>
    internal readonly struct SharpOperator : IAv1InterPredictorOperator
    {
        /// <inheritdoc/>
        public static ReadOnlySpan<short> GetCoefficients(int phase, bool useReducedFilter)
        {
            // AV1 defines sharp filtering on four-sample blocks to be identical to its reduced regular filter.
            // Selecting that table here removes the distinction before the hot traversal is instantiated.
            return GetPhase(useReducedFilter ? RegularFourTap : SharpEightTap, phase);
        }
    }
}
