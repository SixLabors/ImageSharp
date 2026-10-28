// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1FilterIntraPredictorBase
{
    /// <summary>
    /// Defines the AV1 horizontal filter-intra coefficient operator.
    /// </summary>
    internal readonly struct HorizontalOperator : IAv1FilterIntraPredictionOperator
    {
        /// <inheritdoc/>
        public static Av1FilterIntraMode Mode => Av1FilterIntraMode.Horizontal;

        /// <inheritdoc/>
        public static ReadOnlySpan<sbyte> Taps =>
        [
            -8, 8, 0, 0, 0, 16, 0,
        -8, 0, 8, 0, 0, 16, 0,
        -8, 0, 0, 8, 0, 16, 0,
        -8, 0, 0, 0, 8, 16, 0,
        -4, 4, 0, 0, 0, 0, 16,
        -4, 0, 4, 0, 0, 0, 16,
        -4, 0, 0, 4, 0, 0, 16,
        -4, 0, 0, 0, 4, 0, 16,
    ];
    }
}
