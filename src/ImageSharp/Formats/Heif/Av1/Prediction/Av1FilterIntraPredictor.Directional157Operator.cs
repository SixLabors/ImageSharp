// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1FilterIntraPredictorBase
{
    /// <summary>
    /// Defines the AV1 157-degree directional filter-intra coefficient operator.
    /// </summary>
    internal readonly struct Directional157Operator : IAv1FilterIntraPredictionOperator
    {
        /// <inheritdoc/>
        public static Av1FilterIntraMode Mode => Av1FilterIntraMode.Directional157;

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Taps =>
        [
            -2, 8, 0, 0, 0, 10, 0,
        -1, 3, 8, 0, 0, 6, 0,
        -1, 2, 3, 8, 0, 4, 0,
        0, 1, 2, 3, 8, 2, 0,
        -1, 4, 0, 0, 0, 3, 10,
        -1, 3, 4, 0, 0, 4, 6,
        -1, 2, 3, 4, 0, 4, 4,
        -1, 2, 2, 3, 4, 3, 3,
    ];
    }
}
