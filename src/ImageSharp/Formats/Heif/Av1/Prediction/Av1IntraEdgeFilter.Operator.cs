// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Smooths AV1 intra-reference edges while preserving their common-corner sample.
/// </summary>
internal static partial class Av1IntraEdgeFilter
{
    /// <summary>
    /// The sample count required for a maximal edge and its repeated endpoints.
    /// </summary>
    public const int ScratchLength = (2 * Av1Constants.MaxTransformSize) + 4;

    /// <summary>
    /// Defines the rounded smoothing arithmetic for one AV1 filter strength.
    /// </summary>
    internal interface IEdgeFilterOperator
    {
        /// <summary>
        /// Filters one sample using the five neighboring positions.
        /// </summary>
        /// <param name="a">The samples two positions before the output.</param>
        /// <param name="b">The preceding samples.</param>
        /// <param name="c">The centered samples.</param>
        /// <param name="d">The following samples.</param>
        /// <param name="e">The samples two positions after the output.</param>
        /// <returns>The rounded filtered samples.</returns>
        public static abstract int Apply(int a, int b, int c, int d, int e);

        /// <summary>
        /// Filters eight samples using the five neighboring positions.
        /// </summary>
        /// <param name="a">The samples two positions before the output.</param>
        /// <param name="b">The preceding samples.</param>
        /// <param name="c">The centered samples.</param>
        /// <param name="d">The following samples.</param>
        /// <param name="e">The samples two positions after the output.</param>
        /// <returns>The rounded filtered samples.</returns>
        public static abstract Vector128<ushort> Apply(Vector128<ushort> a, Vector128<ushort> b, Vector128<ushort> c, Vector128<ushort> d, Vector128<ushort> e);

        /// <summary>
        /// Filters sixteen samples using the five neighboring positions.
        /// </summary>
        /// <param name="a">The samples two positions before the output.</param>
        /// <param name="b">The preceding samples.</param>
        /// <param name="c">The centered samples.</param>
        /// <param name="d">The following samples.</param>
        /// <param name="e">The samples two positions after the output.</param>
        /// <returns>The rounded filtered samples.</returns>
        public static abstract Vector256<ushort> Apply(Vector256<ushort> a, Vector256<ushort> b, Vector256<ushort> c, Vector256<ushort> d, Vector256<ushort> e);

        /// <summary>
        /// Filters thirty-two samples using the five neighboring positions.
        /// </summary>
        /// <param name="a">The samples two positions before the output.</param>
        /// <param name="b">The preceding samples.</param>
        /// <param name="c">The centered samples.</param>
        /// <param name="d">The following samples.</param>
        /// <param name="e">The samples two positions after the output.</param>
        /// <returns>The rounded filtered samples.</returns>
        public static abstract Vector512<ushort> Apply(Vector512<ushort> a, Vector512<ushort> b, Vector512<ushort> c, Vector512<ushort> d, Vector512<ushort> e);
    }

    /// <summary>
    /// Filters an edge in place, leaving its first sample unchanged.
    /// </summary>
    /// <param name="edge">The first edge sample, including the common corner when present.</param>
    /// <param name="count">The number of edge samples.</param>
    /// <param name="strength">The smoothing strength from zero through three.</param>
    /// <param name="scratch">The source workspace with at least <see cref="ScratchLength"/> samples.</param>
    public static void Apply(ref byte edge, int count, int strength, Span<byte> scratch)
    {
        switch (strength)
        {
            case 1:
                Filter<Strength1Operator>.Apply(ref edge, count, scratch);
                break;
            case 2:
                Filter<Strength2Operator>.Apply(ref edge, count, scratch);
                break;
            case 3:
                Filter<Strength3Operator>.Apply(ref edge, count, scratch);
                break;
        }
    }

    /// <summary>
    /// Filters an edge in place, leaving its first sample unchanged.
    /// </summary>
    /// <param name="edge">The first edge sample, including the common corner when present.</param>
    /// <param name="count">The number of edge samples.</param>
    /// <param name="strength">The smoothing strength from zero through three.</param>
    /// <param name="scratch">The source workspace with at least <see cref="ScratchLength"/> samples.</param>
    public static void Apply(ref short edge, int count, int strength, Span<short> scratch)
    {
        switch (strength)
        {
            case 1:
                Filter<Strength1Operator>.Apply(ref edge, count, scratch);
                break;
            case 2:
                Filter<Strength2Operator>.Apply(ref edge, count, scratch);
                break;
            case 3:
                Filter<Strength3Operator>.Apply(ref edge, count, scratch);
                break;
        }
    }
}
