// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1FilterIntraPredictorBase
{
    /// <summary>
    /// Defines the AV1 Paeth filter-intra coefficient operator.
    /// </summary>
    internal readonly struct PaethOperator : IAv1FilterIntraPredictionOperator
    {
        /// <inheritdoc/>
        public static Av1FilterIntraMode Mode => Av1FilterIntraMode.Paeth;

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Taps =>
        [
            -12, 14, 0, 0, 0, 14, 0,
        -10, 0, 14, 0, 0, 12, 0,
        -9, 0, 0, 14, 0, 11, 0,
        -8, 0, 0, 0, 14, 10, 0,
        -10, 12, 0, 0, 0, 0, 14,
        -9, 1, 12, 0, 0, 0, 12,
        -8, 0, 0, 12, 0, 1, 11,
        -7, 0, 0, 1, 12, 1, 9,
    ];
    }
}
