// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1FilterIntraPredictorBase
{
    /// <summary>
    /// Defines the AV1 vertical filter-intra coefficient operator.
    /// </summary>
    internal readonly struct VerticalOperator : IAv1FilterIntraPredictionOperator
    {
        /// <inheritdoc/>
        public static Av1FilterIntraMode Mode => Av1FilterIntraMode.Vertical;

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Taps =>
        [
            -10, 16, 0, 0, 0, 10, 0,
        -6, 0, 16, 0, 0, 6, 0,
        -4, 0, 0, 16, 0, 4, 0,
        -2, 0, 0, 0, 16, 2, 0,
        -10, 16, 0, 0, 0, 0, 10,
        -6, 0, 16, 0, 0, 0, 6,
        -4, 0, 0, 16, 0, 0, 4,
        -2, 0, 0, 0, 16, 0, 2,
    ];
    }
}
