// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Defines the HEVC intra-prediction operator contract.
/// </content>
internal static partial class HevcIntraPredictor
{
    /// <summary>
    /// Defines one closed intra-prediction operation selected by the decoded mode.
    /// </summary>
    private interface IHevcIntraPredictionOperator
    {
        /// <summary>
        /// Reconstructs one square prediction block.
        /// </summary>
        /// <param name="top">The top-left, top, and top-right reference samples.</param>
        /// <param name="left">The top-left, left, and below-left reference samples.</param>
        /// <param name="destination">The destination buffer beginning at the block origin.</param>
        /// <param name="destinationStride">The destination row stride in samples.</param>
        /// <param name="size">The square block side in samples.</param>
        /// <param name="mode">The decoded prediction mode.</param>
        /// <param name="bitDepth">The reconstructed component precision.</param>
        /// <param name="filterPredictionEdges">Whether the luma edge filter applies to the selected block.</param>
        /// <param name="scratch">The caller-owned block and extended-reference scratch space.</param>
        public static abstract void Predict(
            ReadOnlySpan<ushort> top,
            ReadOnlySpan<ushort> left,
            Span<ushort> destination,
            int destinationStride,
            int size,
            int mode,
            int bitDepth,
            bool filterPredictionEdges,
            Span<ushort> scratch);
    }
}
