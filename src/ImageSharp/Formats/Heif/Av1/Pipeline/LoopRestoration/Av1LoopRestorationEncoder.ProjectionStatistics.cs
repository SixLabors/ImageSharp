// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Measures the self-guided projection statistics and errors across register widths.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Accumulates the self-guided projection moments and errors of consecutive samples in 64-bit lane totals.
    /// </summary>
    /// <remarks>
    /// One 32-bit lane is one sample. A filtered value differs from its Q4 center by up to 2^17, so products need 64
    /// bits; they come from signed widening multiplies of the even and the odd lanes. A total spreads across its lanes
    /// in no defined way; only its lane sum is defined.
    /// </remarks>
    private interface IProjectionStatisticsOperator
    {
        /// <summary>
        /// Adds the projection moments of sixteen samples. Reference: calc_proj_params_r0_r1_c() and its one-radius
        /// forms.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
        /// <param name="original">The first original sample.</param>
        /// <param name="reconstructed">The first reconstructed sample.</param>
        /// <param name="first">The first radius-two filtered value.</param>
        /// <param name="second">The first radius-one filtered value.</param>
        /// <param name="moments">The five lane totals: H00, H01, H11, C0 and C1.</param>
        public static abstract void AccumulateMoments<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ref InlineArray5<Vector512<long>> moments)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator;

        /// <summary>
        /// Adds the projection moments of eight samples.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
        /// <param name="original">The first original sample.</param>
        /// <param name="reconstructed">The first reconstructed sample.</param>
        /// <param name="first">The first radius-two filtered value.</param>
        /// <param name="second">The first radius-one filtered value.</param>
        /// <param name="moments">The five lane totals: H00, H01, H11, C0 and C1.</param>
        public static abstract void AccumulateMoments<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ref InlineArray5<Vector256<long>> moments)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator;

        /// <summary>
        /// Adds the projection moments of four samples.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
        /// <param name="original">The first original sample.</param>
        /// <param name="reconstructed">The first reconstructed sample.</param>
        /// <param name="first">The first radius-two filtered value.</param>
        /// <param name="second">The first radius-one filtered value.</param>
        /// <param name="moments">The five lane totals: H00, H01, H11, C0 and C1.</param>
        public static abstract void AccumulateMoments<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ref InlineArray5<Vector128<long>> moments)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator;

        /// <summary>
        /// Adds the projection moments of one sample.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
        /// <param name="original">The original sample.</param>
        /// <param name="reconstructed">The reconstructed sample.</param>
        /// <param name="first">The radius-two filtered value.</param>
        /// <param name="second">The radius-one filtered value.</param>
        /// <param name="moments">The five totals: H00, H01, H11, C0 and C1.</param>
        public static abstract void AccumulateMoments<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ref InlineArray5<long> moments)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator;

        /// <summary>
        /// Adds the squared projection errors of sixteen samples. Reference: av1_lowbd_pixel_proj_error() and
        /// av1_highbd_pixel_proj_error().
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
        /// <param name="original">The first original sample.</param>
        /// <param name="reconstructed">The first reconstructed sample.</param>
        /// <param name="first">The first radius-two filtered value.</param>
        /// <param name="second">The first radius-one filtered value.</param>
        /// <param name="weights">The decoded radius-two and radius-one weights.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector512<long> AccumulateError<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ProjectionWeights weights,
            Vector512<long> total)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator;

        /// <summary>
        /// Adds the squared projection errors of eight samples.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
        /// <param name="original">The first original sample.</param>
        /// <param name="reconstructed">The first reconstructed sample.</param>
        /// <param name="first">The first radius-two filtered value.</param>
        /// <param name="second">The first radius-one filtered value.</param>
        /// <param name="weights">The decoded radius-two and radius-one weights.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector256<long> AccumulateError<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ProjectionWeights weights,
            Vector256<long> total)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator;

        /// <summary>
        /// Adds the squared projection errors of four samples.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
        /// <param name="original">The first original sample.</param>
        /// <param name="reconstructed">The first reconstructed sample.</param>
        /// <param name="first">The first radius-two filtered value.</param>
        /// <param name="second">The first radius-one filtered value.</param>
        /// <param name="weights">The decoded radius-two and radius-one weights.</param>
        /// <param name="total">The lane totals.</param>
        /// <returns>The updated lane totals.</returns>
        public static abstract Vector128<long> AccumulateError<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ProjectionWeights weights,
            Vector128<long> total)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator;

        /// <summary>
        /// Adds the squared projection error of one sample.
        /// </summary>
        /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
        /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
        /// <param name="original">The original sample.</param>
        /// <param name="reconstructed">The reconstructed sample.</param>
        /// <param name="first">The radius-two filtered value.</param>
        /// <param name="second">The radius-one filtered value.</param>
        /// <param name="weights">The decoded radius-two and radius-one weights.</param>
        /// <param name="total">The total.</param>
        /// <returns>The updated total.</returns>
        public static abstract long AccumulateError<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ProjectionWeights weights,
            long total)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator;
    }

    /// <summary>
    /// Measures the projection moments and the squared projection error of one row, as the self-guided search does
    /// for each row of a unit.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <param name="original">The original samples.</param>
    /// <param name="reconstructed">The reconstructed samples.</param>
    /// <param name="first">The radius-two filtered values.</param>
    /// <param name="second">The radius-one filtered values.</param>
    /// <param name="usesRadiusTwo">Whether the radius-two values contribute.</param>
    /// <param name="usesRadiusOne">Whether the radius-one values contribute.</param>
    /// <param name="firstWeight">The decoded radius-two weight.</param>
    /// <param name="secondWeight">The decoded radius-one weight.</param>
    /// <param name="moments">Receives H00, H01, H11, C0 and C1.</param>
    /// <returns>The squared projection error.</returns>
    internal static long MeasureProjectionRow<TSample>(
        ReadOnlySpan<TSample> original,
        ReadOnlySpan<TSample> reconstructed,
        ReadOnlySpan<int> first,
        ReadOnlySpan<int> second,
        bool usesRadiusTwo,
        bool usesRadiusOne,
        int firstWeight,
        int secondWeight,
        Span<long> moments)
        where TSample : unmanaged
    {
        if (usesRadiusTwo && usesRadiusOne)
        {
            return MeasureProjectionRow<TSample, DualRadiusProjection>(original, reconstructed, first, second, firstWeight, secondWeight, moments);
        }

        return usesRadiusTwo
            ? MeasureProjectionRow<TSample, RadiusTwoProjection>(original, reconstructed, first, second, firstWeight, secondWeight, moments)
            : MeasureProjectionRow<TSample, RadiusOneProjection>(original, reconstructed, first, second, firstWeight, secondWeight, moments);
    }

    /// <summary>
    /// Measures the weighted sum of centered products of two regions, as the Wiener statistics do for each pair of
    /// window positions.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <param name="first">The first region's first sample.</param>
    /// <param name="firstStride">The first region's row stride.</param>
    /// <param name="second">The second region's first sample.</param>
    /// <param name="secondStride">The second region's row stride.</param>
    /// <param name="width">The region width.</param>
    /// <param name="height">The region height.</param>
    /// <param name="average">The common reconstructed-sample average.</param>
    /// <param name="rowStep">The row sampling interval.</param>
    /// <returns>The weighted sum of centered products.</returns>
    internal static long MeasureCorrelation<TSample>(
        ReadOnlySpan<TSample> first,
        int firstStride,
        ReadOnlySpan<TSample> second,
        int secondStride,
        int width,
        int height,
        int average,
        int rowStep)
        where TSample : unmanaged
        => Correlate(first, firstStride, second, secondStride, width, height, average, rowStep);

    /// <summary>
    /// Sums the samples of a region and their squares, as the Wiener mean and the variance pruning do.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <param name="region">The region.</param>
    /// <param name="squares">Receives the sum of the squared samples.</param>
    /// <returns>The sample sum.</returns>
    internal static long MeasureSampleMoments<TSample>(Av1PlaneRegion<TSample> region, out long squares)
        where TSample : unmanaged
    {
        GetSampleMoments(region, out long sum, out squares);
        return sum;
    }

    /// <summary>
    /// Measures the projection moments and the squared projection error of one row for one radius combination.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame sample precision.</typeparam>
    /// <typeparam name="TProjection">The active self-guided radius combination.</typeparam>
    /// <param name="original">The original samples.</param>
    /// <param name="reconstructed">The reconstructed samples.</param>
    /// <param name="first">The radius-two filtered values.</param>
    /// <param name="second">The radius-one filtered values.</param>
    /// <param name="firstWeight">The decoded radius-two weight.</param>
    /// <param name="secondWeight">The decoded radius-one weight.</param>
    /// <param name="moments">Receives H00, H01, H11, C0 and C1.</param>
    /// <returns>The squared projection error.</returns>
    private static long MeasureProjectionRow<TSample, TProjection>(
        ReadOnlySpan<TSample> original,
        ReadOnlySpan<TSample> reconstructed,
        ReadOnlySpan<int> first,
        ReadOnlySpan<int> second,
        int firstWeight,
        int secondWeight,
        Span<long> moments)
        where TSample : unmanaged
        where TProjection : struct, IProjectionOperator
    {
        ProjectionMoments totals = default;
        AccumulateProjectionMoments<TSample, TProjection, ProjectionStatisticsOperator>(original, reconstructed, first, second, ref totals);
        for (int index = 0; index < 5; index++)
        {
            moments[index] = totals.Sum(index);
        }

        LaneTotals error = default;
        AccumulateProjectionError<TSample, TProjection, ProjectionStatisticsOperator>(
            original, reconstructed, first, second, new ProjectionWeights(firstWeight, secondWeight), ref error);

        return error.Sum();
    }

    /// <summary>
    /// Measures projection statistics lane by lane.
    /// </summary>
    private readonly struct ProjectionStatisticsOperator : IProjectionStatisticsOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ref InlineArray5<Vector512<long>> moments)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator
        {
            Vector512<int> center = Av1RestorationSampleOperations.LoadToInt32(ref reconstructed, Vector512<int>.Zero) << 4;
            Vector512<int> target = (Av1RestorationSampleOperations.LoadToInt32(ref original, Vector512<int>.Zero) << 4) - center;
            Vector512<int> a = TProjection.UsesRadiusTwo ? Vector512.LoadUnsafe(ref first) - center : Vector512<int>.Zero;
            Vector512<int> b = TProjection.UsesRadiusOne ? Vector512.LoadUnsafe(ref second) - center : Vector512<int>.Zero;
            moments[0] += MultiplyWidening(a, a);
            moments[1] += MultiplyWidening(a, b);
            moments[2] += MultiplyWidening(b, b);
            moments[3] += MultiplyWidening(a, target);
            moments[4] += MultiplyWidening(b, target);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ref InlineArray5<Vector256<long>> moments)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator
        {
            Vector256<int> center = Av1RestorationSampleOperations.LoadToInt32(ref reconstructed, Vector256<int>.Zero) << 4;
            Vector256<int> target = (Av1RestorationSampleOperations.LoadToInt32(ref original, Vector256<int>.Zero) << 4) - center;
            Vector256<int> a = TProjection.UsesRadiusTwo ? Vector256.LoadUnsafe(ref first) - center : Vector256<int>.Zero;
            Vector256<int> b = TProjection.UsesRadiusOne ? Vector256.LoadUnsafe(ref second) - center : Vector256<int>.Zero;
            moments[0] += MultiplyWidening(a, a);
            moments[1] += MultiplyWidening(a, b);
            moments[2] += MultiplyWidening(b, b);
            moments[3] += MultiplyWidening(a, target);
            moments[4] += MultiplyWidening(b, target);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ref InlineArray5<Vector128<long>> moments)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator
        {
            Vector128<int> center = Av1RestorationSampleOperations.LoadToInt32(ref reconstructed, Vector128<int>.Zero) << 4;
            Vector128<int> target = (Av1RestorationSampleOperations.LoadToInt32(ref original, Vector128<int>.Zero) << 4) - center;
            Vector128<int> a = TProjection.UsesRadiusTwo ? Vector128.LoadUnsafe(ref first) - center : Vector128<int>.Zero;
            Vector128<int> b = TProjection.UsesRadiusOne ? Vector128.LoadUnsafe(ref second) - center : Vector128<int>.Zero;
            moments[0] += MultiplyWidening(a, a);
            moments[1] += MultiplyWidening(a, b);
            moments[2] += MultiplyWidening(b, b);
            moments[3] += MultiplyWidening(a, target);
            moments[4] += MultiplyWidening(b, target);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateMoments<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ref InlineArray5<long> moments)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator
        {
            int center = Av1RestorationSampleOperations.Load(reconstructed) << 4;
            int target = (Av1RestorationSampleOperations.Load(original) << 4) - center;
            int a = TProjection.UsesRadiusTwo ? first - center : 0;
            int b = TProjection.UsesRadiusOne ? second - center : 0;
            moments[0] += (long)a * a;
            moments[1] += (long)a * b;
            moments[2] += (long)b * b;
            moments[3] += (long)a * target;
            moments[4] += (long)b * target;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<long> AccumulateError<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ProjectionWeights weights,
            Vector512<long> total)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator
        {
            // The Q4 center times the Q7 unit weight is a whole multiple of 2^11, so it leaves the rounding shift as
            // the center itself.
            Vector512<int> center = Av1RestorationSampleOperations.LoadToInt32(ref reconstructed, Vector512<int>.Zero);
            Vector512<int> scaled = center << 4;
            Vector512<int> difference = Vector512.Create(1 << 10);
            if (TProjection.UsesRadiusTwo)
            {
                difference += Vector512.Create(weights.First) * (Vector512.LoadUnsafe(ref first) - scaled);
            }

            if (TProjection.UsesRadiusOne)
            {
                difference += Vector512.Create(weights.Second) * (Vector512.LoadUnsafe(ref second) - scaled);
            }

            Vector512<int> error = (difference >> 11) + center - Av1RestorationSampleOperations.LoadToInt32(ref original, Vector512<int>.Zero);
            return total + MultiplyWidening(error, error);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<long> AccumulateError<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ProjectionWeights weights,
            Vector256<long> total)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator
        {
            Vector256<int> center = Av1RestorationSampleOperations.LoadToInt32(ref reconstructed, Vector256<int>.Zero);
            Vector256<int> scaled = center << 4;
            Vector256<int> difference = Vector256.Create(1 << 10);
            if (TProjection.UsesRadiusTwo)
            {
                difference += Vector256.Create(weights.First) * (Vector256.LoadUnsafe(ref first) - scaled);
            }

            if (TProjection.UsesRadiusOne)
            {
                difference += Vector256.Create(weights.Second) * (Vector256.LoadUnsafe(ref second) - scaled);
            }

            Vector256<int> error = (difference >> 11) + center - Av1RestorationSampleOperations.LoadToInt32(ref original, Vector256<int>.Zero);
            return total + MultiplyWidening(error, error);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<long> AccumulateError<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ProjectionWeights weights,
            Vector128<long> total)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator
        {
            Vector128<int> center = Av1RestorationSampleOperations.LoadToInt32(ref reconstructed, Vector128<int>.Zero);
            Vector128<int> scaled = center << 4;
            Vector128<int> difference = Vector128.Create(1 << 10);
            if (TProjection.UsesRadiusTwo)
            {
                difference += Vector128.Create(weights.First) * (Vector128.LoadUnsafe(ref first) - scaled);
            }

            if (TProjection.UsesRadiusOne)
            {
                difference += Vector128.Create(weights.Second) * (Vector128.LoadUnsafe(ref second) - scaled);
            }

            Vector128<int> error = (difference >> 11) + center - Av1RestorationSampleOperations.LoadToInt32(ref original, Vector128<int>.Zero);
            return total + MultiplyWidening(error, error);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AccumulateError<TSample, TProjection>(
            ref TSample original,
            ref TSample reconstructed,
            ref int first,
            ref int second,
            ProjectionWeights weights,
            long total)
            where TSample : unmanaged
            where TProjection : struct, IProjectionOperator
        {
            int center = Av1RestorationSampleOperations.Load(reconstructed);
            int difference = 1 << 10;
            if (TProjection.UsesRadiusTwo)
            {
                difference += weights.First * (first - (center << 4));
            }

            if (TProjection.UsesRadiusOne)
            {
                difference += weights.Second * (second - (center << 4));
            }

            int error = (difference >> 11) + center - Av1RestorationSampleOperations.Load(original);
            return total + ((long)error * error);
        }

        /// <summary>
        /// Multiplies signed 32-bit lanes into 64-bit products and adds each even product to its odd neighbor.
        /// </summary>
        /// <param name="left">The first factors.</param>
        /// <param name="right">The second factors.</param>
        /// <returns>The pairwise product sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<long> MultiplyWidening(Vector512<int> left, Vector512<int> right)
            => Vector512_.MultiplyWideningEven(left, right)
            + Vector512_.MultiplyWideningEven((left.AsInt64() >> 32).AsInt32(), (right.AsInt64() >> 32).AsInt32());

        /// <inheritdoc cref="MultiplyWidening(Vector512{int}, Vector512{int})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<long> MultiplyWidening(Vector256<int> left, Vector256<int> right)
            => Vector256_.MultiplyWideningEven(left, right)
            + Vector256_.MultiplyWideningEven((left.AsInt64() >> 32).AsInt32(), (right.AsInt64() >> 32).AsInt32());

        /// <inheritdoc cref="MultiplyWidening(Vector512{int}, Vector512{int})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<long> MultiplyWidening(Vector128<int> left, Vector128<int> right)
            => Vector128_.MultiplyWideningEven(left, right)
            + Vector128_.MultiplyWideningEven((left.AsInt64() >> 32).AsInt32(), (right.AsInt64() >> 32).AsInt32());
    }

    /// <summary>
    /// Carries the five projection moments of a unit in the lane totals of every register width.
    /// </summary>
    private struct ProjectionMoments
    {
        /// <summary>
        /// The 512-bit lane totals of H00, H01, H11, C0 and C1.
        /// </summary>
        public InlineArray5<Vector512<long>> Lanes512;

        /// <summary>
        /// The 256-bit lane totals of H00, H01, H11, C0 and C1.
        /// </summary>
        public InlineArray5<Vector256<long>> Lanes256;

        /// <summary>
        /// The 128-bit lane totals of H00, H01, H11, C0 and C1.
        /// </summary>
        public InlineArray5<Vector128<long>> Lanes128;

        /// <summary>
        /// The scalar totals of H00, H01, H11, C0 and C1.
        /// </summary>
        public InlineArray5<long> Scalar;

        /// <summary>
        /// Reduces one moment across every register width.
        /// </summary>
        /// <param name="index">The moment: 0 for H00, 1 for H01, 2 for H11, 3 for C0 and 4 for C1.</param>
        /// <returns>The exact moment.</returns>
        public readonly long Sum(int index)
            => Vector512.Sum(this.Lanes512[index]) + Vector256.Sum(this.Lanes256[index]) + Vector128.Sum(this.Lanes128[index]) + this.Scalar[index];
    }

    /// <summary>
    /// Carries a 64-bit total in the lane totals of every register width.
    /// </summary>
    private struct LaneTotals
    {
        /// <summary>
        /// The 512-bit lane totals.
        /// </summary>
        public Vector512<long> Lanes512;

        /// <summary>
        /// The 256-bit lane totals.
        /// </summary>
        public Vector256<long> Lanes256;

        /// <summary>
        /// The 128-bit lane totals.
        /// </summary>
        public Vector128<long> Lanes128;

        /// <summary>
        /// The scalar total.
        /// </summary>
        public long Scalar;

        /// <summary>
        /// Reduces the total across every register width.
        /// </summary>
        /// <returns>The exact total.</returns>
        public readonly long Sum()
            => Vector512.Sum(this.Lanes512) + Vector256.Sum(this.Lanes256) + Vector128.Sum(this.Lanes128) + this.Scalar;
    }

    /// <summary>
    /// Holds the decoded projection weights of one candidate.
    /// </summary>
    private readonly struct ProjectionWeights
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectionWeights"/> struct.
        /// </summary>
        /// <param name="first">The radius-two weight.</param>
        /// <param name="second">The radius-one weight.</param>
        public ProjectionWeights(int first, int second)
        {
            this.First = first;
            this.Second = second;
        }

        /// <summary>
        /// Gets the radius-two weight.
        /// </summary>
        public int First { get; }

        /// <summary>
        /// Gets the radius-one weight.
        /// </summary>
        public int Second { get; }
    }
}
