// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1FilterIntraPredictorBase
{
    /// <summary>
    /// Defines the AV1 DC filter-intra coefficient operator.
    /// </summary>
    internal readonly struct DcOperator : IAv1FilterIntraPredictionOperator
    {
        /// <inheritdoc/>
        public static Av1FilterIntraMode Mode => Av1FilterIntraMode.DC;

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Taps =>
        [
            -6, 10, 0, 0, 0, 12, 0,
        -5, 2, 10, 0, 0, 9, 0,
        -3, 1, 1, 10, 0, 7, 0,
        -3, 1, 1, 2, 10, 5, 0,
        -4, 6, 0, 0, 0, 2, 12,
        -3, 2, 6, 0, 0, 2, 9,
        -3, 2, 2, 6, 0, 2, 7,
        -3, 1, 2, 2, 6, 3, 5,
    ];
    }
}
