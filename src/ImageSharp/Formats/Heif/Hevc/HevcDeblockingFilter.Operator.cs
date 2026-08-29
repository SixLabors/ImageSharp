// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcDeblockingFilter
{
    /// <summary>
    /// Defines orientation-specific access to the four samples running along one deblocking edge segment.
    /// </summary>
    private interface IEdgeOperator
    {
        /// <summary>
        /// Loads samples at one signed distance across the edge.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="distance">The signed sample distance across the edge.</param>
        /// <param name="count">The number of valid low lanes to load.</param>
        /// <returns>The widened samples ordered along the edge.</returns>
        public static abstract Vector128<int> LoadVector(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int count);

        /// <summary>
        /// Stores four samples at one signed distance across the edge.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="distance">The signed sample distance across the edge.</param>
        /// <param name="value">The four widened samples ordered along the edge.</param>
        /// <param name="count">The number of low lanes to store.</param>
        public static abstract void StoreVector(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int distance,
            Vector128<int> value,
            int count);

        /// <summary>
        /// Loads one scalar sample at a signed distance across and an offset along the edge.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="distance">The signed sample distance across the edge.</param>
        /// <param name="index">The sample offset along the edge.</param>
        /// <returns>The selected sample.</returns>
        public static abstract int LoadScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index);

        /// <summary>
        /// Stores one scalar sample at a signed distance across and an offset along the edge.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="distance">The signed sample distance across the edge.</param>
        /// <param name="index">The sample offset along the edge.</param>
        /// <param name="value">The filtered sample.</param>
        public static abstract void StoreScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index, int value);
    }
}
