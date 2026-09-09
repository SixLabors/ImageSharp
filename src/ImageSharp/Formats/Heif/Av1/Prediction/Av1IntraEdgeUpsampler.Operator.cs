// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Inserts clipped half-sample positions into AV1 intra-reference edges.
/// </summary>
internal static partial class Av1IntraEdgeUpsampler
{
    /// <summary>
    /// The maximum number of original samples permitted in an upsampled edge.
    /// </summary>
    public const int MaximumCount = 16;

    /// <summary>
    /// The sample count required for the original edge, corner, and repeated endpoints.
    /// </summary>
    public const int ScratchLength = MaximumCount + 3;

    /// <summary>
    /// Defines signed four-tap interpolation before sample narrowing.
    /// </summary>
    internal interface IEdgeUpsamplingOperator
    {
        /// <summary>
        /// Interpolates half samples and clamps them to the coded range.
        /// </summary>
        /// <param name="a">The preceding samples.</param>
        /// <param name="b">The first central samples.</param>
        /// <param name="c">The second central samples.</param>
        /// <param name="d">The following samples.</param>
        /// <param name="maximum">The maximum coded sample.</param>
        /// <returns>The rounded and clipped half samples.</returns>
        public static abstract int Interpolate(int a, int b, int c, int d, int maximum);

        /// <summary>
        /// Interpolates half samples and clamps them to the coded range.
        /// </summary>
        /// <param name="a">The preceding samples.</param>
        /// <param name="b">The first central samples.</param>
        /// <param name="c">The second central samples.</param>
        /// <param name="d">The following samples.</param>
        /// <param name="maximum">The maximum coded sample.</param>
        /// <returns>The rounded and clipped half samples.</returns>
        public static abstract Vector128<int> Interpolate(Vector128<int> a, Vector128<int> b, Vector128<int> c, Vector128<int> d, int maximum);

        /// <summary>
        /// Interpolates half samples and clamps them to the coded range.
        /// </summary>
        /// <param name="a">The preceding samples.</param>
        /// <param name="b">The first central samples.</param>
        /// <param name="c">The second central samples.</param>
        /// <param name="d">The following samples.</param>
        /// <param name="maximum">The maximum coded sample.</param>
        /// <returns>The rounded and clipped half samples.</returns>
        public static abstract Vector256<int> Interpolate(Vector256<int> a, Vector256<int> b, Vector256<int> c, Vector256<int> d, int maximum);

        /// <summary>
        /// Interpolates half samples and clamps them to the coded range.
        /// </summary>
        /// <param name="a">The preceding samples.</param>
        /// <param name="b">The first central samples.</param>
        /// <param name="c">The second central samples.</param>
        /// <param name="d">The following samples.</param>
        /// <param name="maximum">The maximum coded sample.</param>
        /// <returns>The rounded and clipped half samples.</returns>
        public static abstract Vector512<int> Interpolate(Vector512<int> a, Vector512<int> b, Vector512<int> c, Vector512<int> d, int maximum);
    }

    /// <summary>
    /// Inserts half samples before each original edge sample.
    /// </summary>
    /// <param name="edge">The edge with writable prefix samples at -2 and -1 and room for the doubled extent.</param>
    /// <param name="count">The number of original edge samples, at most <see cref="MaximumCount"/>.</param>
    /// <param name="scratch">The original-sample workspace with at least <see cref="ScratchLength"/> samples.</param>
    public static void Apply(Span<byte> edge, int count, Span<byte> scratch)
        => Upsampler<FourTapOperator>.Apply(edge, count, scratch);

    /// <summary>
    /// Inserts half samples before each original edge sample.
    /// </summary>
    /// <param name="edge">The edge with writable prefix samples at -2 and -1 and room for the doubled extent.</param>
    /// <param name="count">The number of original edge samples, at most <see cref="MaximumCount"/>.</param>
    /// <param name="bitDepth">The coded precision used to clamp interpolation.</param>
    /// <param name="scratch">The original-sample workspace with at least <see cref="ScratchLength"/> samples.</param>
    public static void Apply(Span<short> edge, int count, int bitDepth, Span<short> scratch)
        => Upsampler<FourTapOperator>.Apply(edge, count, (1 << bitDepth) - 1, scratch);
}
