// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefFilter
{
    /// <summary>
    /// Defines storage-specific writes for one filtered row.
    /// </summary>
    /// <typeparam name="TSample">The destination sample storage type.</typeparam>
    private interface IOutputOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Stores four or eight filtered samples from the low vector lanes.
        /// </summary>
        /// <param name="destination">The first element in the destination plane.</param>
        /// <param name="offset">The offset of the first sample to write.</param>
        /// <param name="value">The filtered samples in the low lanes.</param>
        /// <param name="count">The number of valid lanes.</param>
        public static abstract void StoreVector(ref TSample destination, int offset, Vector128<short> value, int count);

        /// <summary>
        /// Stores one filtered sample.
        /// </summary>
        /// <param name="destination">The first element in the destination plane.</param>
        /// <param name="offset">The offset of the sample to write.</param>
        /// <param name="value">The filtered sample.</param>
        public static abstract void StoreScalar(ref TSample destination, int offset, int value);
    }

    /// <summary>
    /// Defines which groups of directional taps participate in one closed filter kernel.
    /// </summary>
    private interface IFilterOperator
    {
        /// <summary>
        /// Gets a value indicating whether the primary directional taps are enabled.
        /// </summary>
        public static abstract bool EnablePrimary { get; }

        /// <summary>
        /// Gets a value indicating whether the secondary off-axis taps are enabled.
        /// </summary>
        public static abstract bool EnableSecondary { get; }
    }
}
