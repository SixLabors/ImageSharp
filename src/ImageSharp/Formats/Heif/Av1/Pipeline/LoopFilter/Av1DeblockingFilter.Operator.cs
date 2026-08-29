// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

internal static partial class Av1DeblockingFilter
{
    /// <summary>
    /// Defines orientation- and storage-specific access to the four samples running along one edge segment.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample storage type.</typeparam>
    private interface IEdgeOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Loads four samples at one signed distance across the edge.
        /// </summary>
        /// <param name="samples">The first element in the plane storage.</param>
        /// <param name="q0Offset">The offset of the first Q-side sample.</param>
        /// <param name="stride">The number of samples between adjacent rows.</param>
        /// <param name="distance">The signed sample distance from Q0.</param>
        /// <returns>The widened samples ordered along the edge.</returns>
        public static abstract Vector128<int> LoadVector(ref TSample samples, int q0Offset, int stride, int distance);

        /// <summary>
        /// Stores four samples at one signed distance across the edge.
        /// </summary>
        /// <param name="samples">The first element in the plane storage.</param>
        /// <param name="q0Offset">The offset of the first Q-side sample.</param>
        /// <param name="stride">The number of samples between adjacent rows.</param>
        /// <param name="distance">The signed sample distance from Q0.</param>
        /// <param name="value">The widened samples ordered along the edge.</param>
        public static abstract void StoreVector(ref TSample samples, int q0Offset, int stride, int distance, Vector128<int> value);

        /// <summary>
        /// Loads one sample at a signed distance across and an offset along the edge.
        /// </summary>
        /// <param name="samples">The first element in the plane storage.</param>
        /// <param name="q0Offset">The offset of the first Q-side sample.</param>
        /// <param name="stride">The number of samples between adjacent rows.</param>
        /// <param name="distance">The signed sample distance from Q0.</param>
        /// <param name="index">The sample offset along the edge.</param>
        /// <returns>The selected sample.</returns>
        public static abstract int LoadScalar(ref TSample samples, int q0Offset, int stride, int distance, int index);

        /// <summary>
        /// Stores one sample at a signed distance across and an offset along the edge.
        /// </summary>
        /// <param name="samples">The first element in the plane storage.</param>
        /// <param name="q0Offset">The offset of the first Q-side sample.</param>
        /// <param name="stride">The number of samples between adjacent rows.</param>
        /// <param name="distance">The signed sample distance from Q0.</param>
        /// <param name="index">The sample offset along the edge.</param>
        /// <param name="value">The filtered sample.</param>
        public static abstract void StoreScalar(ref TSample samples, int q0Offset, int stride, int distance, int index, int value);
    }
}
