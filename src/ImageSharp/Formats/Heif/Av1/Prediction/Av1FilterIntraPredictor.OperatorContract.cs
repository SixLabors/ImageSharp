// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1FilterIntraPredictorBase
{
    /// <summary>
    /// Defines the coefficient set for one AV1 filter-intra prediction mode.
    /// </summary>
    internal interface IAv1FilterIntraPredictionOperator
    {
        /// <summary>
        /// Gets the filter-intra mode implemented by the operator.
        /// </summary>
        public static abstract Av1FilterIntraMode Mode { get; }

        /// <summary>
        /// Gets the eight seven-tap coefficient rows used by the operator.
        /// </summary>
        public static abstract ReadOnlySpan<sbyte> Taps { get; }
    }
}
