// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <content>
/// Defines the sample-width-specific arithmetic of the temporal filter traversals.
/// </content>
internal static partial class Av1TemporalFilter
{
    /// <summary>
    /// Defines the per-sample temporal filter arithmetic for one sample storage type across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <remarks>
    /// <para>
    /// Every kernel has a 128-bit, a 256-bit and a 512-bit overload and a scalar overload. The traversals in
    /// <see cref="Av1TemporalFilter"/> walk the widest accelerated overload first and finish each row in the
    /// scalar overload. The overloads that return nothing are selected by a default-valued <c>lanes</c> argument.
    /// </para>
    /// <para>
    /// Sample kernels widen samples to thirty-two-bit lanes: a squared twelve-bit difference needs twenty-four bits
    /// and a filter accumulator needs twenty-six. The noise kernel is the exception. It keeps sixteen-bit lanes, as
    /// av1_estimate_noise_from_single_plane_avx2() does, because every intermediate Sobel and Laplacian value of a
    /// twelve-bit plane stays inside the signed sixteen-bit range.
    /// </para>
    /// <para>
    /// The weight and normalization kernels convert thirty-two-bit integers to <see cref="double"/> through the
    /// exponent bias of 2^52, which is exact for every unsigned thirty-two-bit value and needs no instruction that
    /// only AVX-512 provides.
    /// </para>
    /// </remarks>
    internal interface ITemporalFilterOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Stores the squared differences of four samples. Reference: get_squared_error_avx2().
        /// </summary>
        /// <param name="frame">The first sample of the frame to filter.</param>
        /// <param name="prediction">The first predicted sample.</param>
        /// <param name="destination">The first squared difference to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void StoreSquaredErrors(ref TSample frame, ref TSample prediction, ref uint destination, Vector128<uint> lanes);

        /// <summary>
        /// Stores the squared differences of eight samples. Reference: get_squared_error_avx2().
        /// </summary>
        /// <param name="frame">The first sample of the frame to filter.</param>
        /// <param name="prediction">The first predicted sample.</param>
        /// <param name="destination">The first squared difference to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void StoreSquaredErrors(ref TSample frame, ref TSample prediction, ref uint destination, Vector256<uint> lanes);

        /// <summary>
        /// Stores the squared differences of sixteen samples. Reference: get_squared_error_avx2().
        /// </summary>
        /// <param name="frame">The first sample of the frame to filter.</param>
        /// <param name="prediction">The first predicted sample.</param>
        /// <param name="destination">The first squared difference to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void StoreSquaredErrors(ref TSample frame, ref TSample prediction, ref uint destination, Vector512<uint> lanes);

        /// <summary>
        /// Stores the squared difference of one sample. Reference: compute_square_diff().
        /// </summary>
        /// <param name="frame">The sample of the frame to filter.</param>
        /// <param name="prediction">The predicted sample.</param>
        /// <param name="destination">The squared difference to write.</param>
        public static abstract void StoreSquaredErrors(TSample frame, TSample prediction, ref uint destination);

        /// <summary>
        /// Adds four samples of the frame to filter at the full filter weight. Reference: tf_apply_temporal_filter_self().
        /// </summary>
        /// <param name="sample">The first sample of the frame to filter.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void AccumulateSelf(ref TSample sample, ref uint accumulator, ref ushort count, Vector128<uint> lanes);

        /// <summary>
        /// Adds eight samples of the frame to filter at the full filter weight. Reference: tf_apply_temporal_filter_self().
        /// </summary>
        /// <param name="sample">The first sample of the frame to filter.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void AccumulateSelf(ref TSample sample, ref uint accumulator, ref ushort count, Vector256<uint> lanes);

        /// <summary>
        /// Adds sixteen samples of the frame to filter at the full filter weight. Reference: tf_apply_temporal_filter_self().
        /// </summary>
        /// <param name="sample">The first sample of the frame to filter.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void AccumulateSelf(ref TSample sample, ref uint accumulator, ref ushort count, Vector512<uint> lanes);

        /// <summary>
        /// Adds one sample of the frame to filter at the full filter weight. Reference: tf_apply_temporal_filter_self().
        /// </summary>
        /// <param name="sample">The sample of the frame to filter.</param>
        /// <param name="accumulator">The weighted sum to update.</param>
        /// <param name="count">The weight total to update.</param>
        public static abstract void AccumulateSelf(TSample sample, ref uint accumulator, ref ushort count);

        /// <summary>
        /// Weighs four predicted samples with the approximated exponential and adds them to the accumulators.
        /// Reference: the tf_wgt_calc_lvl 1 branch of apply_temporal_filter() in av1_apply_temporal_filter_avx2().
        /// </summary>
        /// <param name="windowErrors">The first scaled window error.</param>
        /// <param name="prediction">The first predicted sample.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="terms">The error terms of the sixteen-by-sixteen sub-block that holds the four samples.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void AccumulateWeights(ref uint windowErrors, ref TSample prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector128<double> lanes);

        /// <summary>
        /// Weighs four predicted samples with the approximated exponential and adds them to the accumulators.
        /// Reference: the tf_wgt_calc_lvl 1 branch of apply_temporal_filter() in av1_apply_temporal_filter_avx2().
        /// </summary>
        /// <param name="windowErrors">The first scaled window error.</param>
        /// <param name="prediction">The first predicted sample.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="terms">The error terms of the sixteen-by-sixteen sub-block that holds the four samples.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void AccumulateWeights(ref uint windowErrors, ref TSample prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector256<double> lanes);

        /// <summary>
        /// Weighs eight predicted samples with the approximated exponential and adds them to the accumulators.
        /// Reference: the tf_wgt_calc_lvl 1 branch of apply_temporal_filter() in av1_apply_temporal_filter_avx2().
        /// </summary>
        /// <param name="windowErrors">The first scaled window error.</param>
        /// <param name="prediction">The first predicted sample.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="terms">The error terms of the sixteen-by-sixteen sub-block that holds the eight samples.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void AccumulateWeights(ref uint windowErrors, ref TSample prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector512<double> lanes);

        /// <summary>
        /// Weighs one predicted sample and adds it to the accumulators, with either weight calculation level.
        /// Reference: the per-sample weight of av1_apply_temporal_filter_c() and apply_temporal_filter().
        /// </summary>
        /// <param name="windowError">The scaled window error.</param>
        /// <param name="prediction">The predicted sample.</param>
        /// <param name="accumulator">The weighted sum to update.</param>
        /// <param name="count">The weight total to update.</param>
        /// <param name="terms">The error terms of the sixteen-by-sixteen sub-block that holds the sample.</param>
        public static abstract void AccumulateWeights(uint windowError, TSample prediction, ref uint accumulator, ref ushort count, in WeightTerms terms);

        /// <summary>
        /// Divides four accumulators by their weight totals with rounding. Reference: tf_normalize_filtered_frame().
        /// </summary>
        /// <param name="accumulator">The first weighted sum.</param>
        /// <param name="count">The first weight total.</param>
        /// <param name="destination">The first filtered sample to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void Normalize(ref uint accumulator, ref ushort count, ref TSample destination, Vector128<double> lanes);

        /// <summary>
        /// Divides four accumulators by their weight totals with rounding. Reference: tf_normalize_filtered_frame().
        /// </summary>
        /// <param name="accumulator">The first weighted sum.</param>
        /// <param name="count">The first weight total.</param>
        /// <param name="destination">The first filtered sample to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void Normalize(ref uint accumulator, ref ushort count, ref TSample destination, Vector256<double> lanes);

        /// <summary>
        /// Divides eight accumulators by their weight totals with rounding. Reference: tf_normalize_filtered_frame().
        /// </summary>
        /// <param name="accumulator">The first weighted sum.</param>
        /// <param name="count">The first weight total.</param>
        /// <param name="destination">The first filtered sample to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void Normalize(ref uint accumulator, ref ushort count, ref TSample destination, Vector512<double> lanes);

        /// <summary>
        /// Divides one accumulator by its weight total with rounding. Reference: tf_normalize_filtered_frame().
        /// </summary>
        /// <param name="accumulator">The weighted sum.</param>
        /// <param name="count">The weight total.</param>
        /// <param name="destination">The filtered sample to write.</param>
        public static abstract void Normalize(uint accumulator, ushort count, ref TSample destination);

        /// <summary>
        /// Adds the Laplacian magnitudes of the smooth samples among eight columns to lane totals.
        /// Reference: av1_estimate_noise_from_single_plane_avx2().
        /// </summary>
        /// <param name="center">The first center sample.</param>
        /// <param name="stride">The plane row stride.</param>
        /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
        /// <param name="sum">The running Laplacian total.</param>
        /// <param name="count">The running smooth-sample total.</param>
        public static abstract void AccumulateNoise(ref TSample center, int stride, in NoiseTerms terms, ref Vector128<int> sum, ref Vector128<int> count);

        /// <summary>
        /// Adds the Laplacian magnitudes of the smooth samples among sixteen columns to lane totals.
        /// Reference: av1_estimate_noise_from_single_plane_avx2().
        /// </summary>
        /// <param name="center">The first center sample.</param>
        /// <param name="stride">The plane row stride.</param>
        /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
        /// <param name="sum">The running Laplacian total.</param>
        /// <param name="count">The running smooth-sample total.</param>
        public static abstract void AccumulateNoise(ref TSample center, int stride, in NoiseTerms terms, ref Vector256<int> sum, ref Vector256<int> count);

        /// <summary>
        /// Adds the Laplacian magnitudes of the smooth samples among thirty-two columns to lane totals.
        /// Reference: av1_estimate_noise_from_single_plane_avx2().
        /// </summary>
        /// <param name="center">The first center sample.</param>
        /// <param name="stride">The plane row stride.</param>
        /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
        /// <param name="sum">The running Laplacian total.</param>
        /// <param name="count">The running smooth-sample total.</param>
        public static abstract void AccumulateNoise(ref TSample center, int stride, in NoiseTerms terms, ref Vector512<int> sum, ref Vector512<int> count);

        /// <summary>
        /// Adds the Laplacian magnitude of one sample when it is smooth.
        /// Reference: av1_estimate_noise_from_single_plane_c() and av1_highbd_estimate_noise_from_single_plane_c().
        /// </summary>
        /// <param name="center">The center sample.</param>
        /// <param name="stride">The plane row stride.</param>
        /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
        /// <param name="sum">The running Laplacian total.</param>
        /// <param name="count">The running smooth-sample total.</param>
        public static abstract void AccumulateNoise(ref TSample center, int stride, in NoiseTerms terms, ref int sum, ref int count);

        /// <summary>
        /// Stores the sums of five rows of four squared differences. Reference: the row loop of apply_temporal_filter().
        /// </summary>
        /// <param name="row0">The first value of the top row.</param>
        /// <param name="row1">The first value of the second row.</param>
        /// <param name="row2">The first value of the center row.</param>
        /// <param name="row3">The first value of the fourth row.</param>
        /// <param name="row4">The first value of the bottom row.</param>
        /// <param name="destination">The first column sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector128<uint> lanes);

        /// <summary>
        /// Stores the sums of five rows of eight squared differences. Reference: the row loop of apply_temporal_filter().
        /// </summary>
        /// <param name="row0">The first value of the top row.</param>
        /// <param name="row1">The first value of the second row.</param>
        /// <param name="row2">The first value of the center row.</param>
        /// <param name="row3">The first value of the fourth row.</param>
        /// <param name="row4">The first value of the bottom row.</param>
        /// <param name="destination">The first column sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector256<uint> lanes);

        /// <summary>
        /// Stores the sums of five rows of sixteen squared differences. Reference: the row loop of apply_temporal_filter().
        /// </summary>
        /// <param name="row0">The first value of the top row.</param>
        /// <param name="row1">The first value of the second row.</param>
        /// <param name="row2">The first value of the center row.</param>
        /// <param name="row3">The first value of the fourth row.</param>
        /// <param name="row4">The first value of the bottom row.</param>
        /// <param name="destination">The first column sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector512<uint> lanes);

        /// <summary>
        /// Stores the sum of five rows of one squared difference. Reference: the window loop of av1_apply_temporal_filter_c().
        /// </summary>
        /// <param name="row0">The value of the top row.</param>
        /// <param name="row1">The value of the second row.</param>
        /// <param name="row2">The value of the center row.</param>
        /// <param name="row3">The value of the fourth row.</param>
        /// <param name="row4">The value of the bottom row.</param>
        /// <param name="destination">The column sum to write.</param>
        public static abstract void SumRows(uint row0, uint row1, uint row2, uint row3, uint row4, ref uint destination);

        /// <summary>
        /// Stores four window errors: five adjacent column sums plus the luma error, scaled to eight bits.
        /// Reference: xx_mask_and_hadd() and the diff_sse of apply_temporal_filter().
        /// </summary>
        /// <param name="columns">The column sum two columns left of the first output, in the edge-padded row.</param>
        /// <param name="luma">The first luma error, zero on the luma plane.</param>
        /// <param name="shift">The high-bit-depth scaling shift.</param>
        /// <param name="destination">The first window error to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector128<uint> lanes);

        /// <summary>
        /// Stores eight window errors: five adjacent column sums plus the luma error, scaled to eight bits.
        /// Reference: xx_mask_and_hadd() and the diff_sse of apply_temporal_filter().
        /// </summary>
        /// <param name="columns">The column sum two columns left of the first output, in the edge-padded row.</param>
        /// <param name="luma">The first luma error, zero on the luma plane.</param>
        /// <param name="shift">The high-bit-depth scaling shift.</param>
        /// <param name="destination">The first window error to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector256<uint> lanes);

        /// <summary>
        /// Stores sixteen window errors: five adjacent column sums plus the luma error, scaled to eight bits.
        /// Reference: xx_mask_and_hadd() and the diff_sse of apply_temporal_filter().
        /// </summary>
        /// <param name="columns">The column sum two columns left of the first output, in the edge-padded row.</param>
        /// <param name="luma">The first luma error, zero on the luma plane.</param>
        /// <param name="shift">The high-bit-depth scaling shift.</param>
        /// <param name="destination">The first window error to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector512<uint> lanes);

        /// <summary>
        /// Stores one window error: five adjacent column sums plus the luma error, scaled to eight bits.
        /// Reference: the sum_square_diff of av1_apply_temporal_filter_c().
        /// </summary>
        /// <param name="columns">The column sum two columns left of the output, in the edge-padded row.</param>
        /// <param name="luma">The luma error, zero on the luma plane.</param>
        /// <param name="shift">The high-bit-depth scaling shift.</param>
        /// <param name="destination">The window error to write.</param>
        public static abstract void SumWindow(ref uint columns, uint luma, int shift, ref uint destination);

        /// <summary>
        /// Stores four horizontal luma pair sums. Reference: compute_luma_sq_error_sum() for 4:2:2 subsampling.
        /// </summary>
        /// <param name="row">The first luma squared difference.</param>
        /// <param name="destination">The first chroma-position sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumLumaPairs(ref uint row, ref uint destination, Vector128<uint> lanes);

        /// <summary>
        /// Stores eight horizontal luma pair sums. Reference: compute_luma_sq_error_sum() for 4:2:2 subsampling.
        /// </summary>
        /// <param name="row">The first luma squared difference.</param>
        /// <param name="destination">The first chroma-position sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumLumaPairs(ref uint row, ref uint destination, Vector256<uint> lanes);

        /// <summary>
        /// Stores sixteen horizontal luma pair sums. Reference: compute_luma_sq_error_sum() for 4:2:2 subsampling.
        /// </summary>
        /// <param name="row">The first luma squared difference.</param>
        /// <param name="destination">The first chroma-position sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumLumaPairs(ref uint row, ref uint destination, Vector512<uint> lanes);

        /// <summary>
        /// Stores one horizontal luma pair sum. Reference: compute_luma_sq_error_sum() for 4:2:2 subsampling.
        /// </summary>
        /// <param name="row">The first luma squared difference of the pair.</param>
        /// <param name="destination">The chroma-position sum to write.</param>
        public static abstract void SumLumaPairs(ref uint row, ref uint destination);

        /// <summary>
        /// Stores four two-by-two luma sums. Reference: compute_luma_sq_error_sum() for 4:2:0 subsampling.
        /// </summary>
        /// <param name="upper">The first luma squared difference of the upper row.</param>
        /// <param name="lower">The first luma squared difference of the lower row.</param>
        /// <param name="destination">The first chroma-position sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector128<uint> lanes);

        /// <summary>
        /// Stores eight two-by-two luma sums. Reference: compute_luma_sq_error_sum() for 4:2:0 subsampling.
        /// </summary>
        /// <param name="upper">The first luma squared difference of the upper row.</param>
        /// <param name="lower">The first luma squared difference of the lower row.</param>
        /// <param name="destination">The first chroma-position sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector256<uint> lanes);

        /// <summary>
        /// Stores sixteen two-by-two luma sums. Reference: compute_luma_sq_error_sum() for 4:2:0 subsampling.
        /// </summary>
        /// <param name="upper">The first luma squared difference of the upper row.</param>
        /// <param name="lower">The first luma squared difference of the lower row.</param>
        /// <param name="destination">The first chroma-position sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector512<uint> lanes);

        /// <summary>
        /// Stores one two-by-two luma sum. Reference: compute_luma_sq_error_sum() for 4:2:0 subsampling.
        /// </summary>
        /// <param name="upper">The first luma squared difference of the upper row.</param>
        /// <param name="lower">The first luma squared difference of the lower row.</param>
        /// <param name="destination">The chroma-position sum to write.</param>
        public static abstract void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination);
    }

    /// <summary>
    /// Carries the terms that are constant across one sixteen-by-sixteen sub-block of one plane in the weight kernel.
    /// </summary>
    internal readonly struct WeightTerms
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WeightTerms"/> struct. Reference: apply_temporal_filter().
        /// </summary>
        /// <param name="inverseReferenceCount">The reciprocal of the number of squared errors in a window.</param>
        /// <param name="blockError">The sub-block motion search error, already multiplied by the normalization factor.</param>
        /// <param name="firstFactor">The first multiplier of the combined error.</param>
        /// <param name="secondFactor">The second multiplier of the combined error.</param>
        /// <param name="level">The weight calculation level: zero for the exponential, one for its approximation.</param>
        public WeightTerms(double inverseReferenceCount, double blockError, double firstFactor, double secondFactor, int level)
        {
            this.InverseReferenceCount = inverseReferenceCount;
            this.BlockError = blockError;
            this.FirstFactor = firstFactor;
            this.SecondFactor = secondFactor;
            this.Level = level;
        }

        /// <summary>
        /// Gets the reciprocal of the number of squared errors in a window: 25 on luma, 25 plus the covered luma
        /// samples on chroma.
        /// </summary>
        public double InverseReferenceCount { get; }

        /// <summary>
        /// Gets the sub-block motion search error multiplied by the normalization factor.
        /// </summary>
        public double BlockError { get; }

        /// <summary>
        /// Gets the first multiplier of the combined error.
        /// </summary>
        /// <remarks>
        /// The x64 kernels multiply by the product of the distance factor and the decay factor, and pass one as
        /// <see cref="SecondFactor"/>; multiplying by one is exact. The C kernel, which libaom runs for 4:2:2 high
        /// bit depth, multiplies by the distance factor and then by the decay factor, which rounds twice.
        /// </remarks>
        public double FirstFactor { get; }

        /// <summary>
        /// Gets the second multiplier of the combined error.
        /// </summary>
        public double SecondFactor { get; }

        /// <summary>
        /// Gets the weight calculation level, tf_wgt_calc_lvl: zero uses exp(), one uses approx_exp().
        /// </summary>
        public int Level { get; }
    }

    /// <summary>
    /// Carries the per-plane constants of the noise estimation kernel.
    /// </summary>
    internal readonly struct NoiseTerms
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NoiseTerms"/> struct. Reference:
        /// av1_highbd_estimate_noise_from_single_plane_c().
        /// </summary>
        /// <param name="edgeThreshold">The gradient magnitude below which a sample counts as smooth.</param>
        /// <param name="bitDepth">The sample bit depth.</param>
        public NoiseTerms(int edgeThreshold, int bitDepth)
        {
            this.EdgeThreshold = edgeThreshold;
            this.Shift = bitDepth - 8;
            this.Bias = (1 << this.Shift) >> 1;
        }

        /// <summary>
        /// Gets the gradient magnitude below which a sample counts as smooth, NOISE_ESTIMATION_EDGE_THRESHOLD.
        /// </summary>
        public int EdgeThreshold { get; }

        /// <summary>
        /// Gets the shift that scales high-bit-depth magnitudes to the eight-bit domain.
        /// </summary>
        public int Shift { get; }

        /// <summary>
        /// Gets the rounding bias of <see cref="Shift"/>; ROUND_POWER_OF_TWO() adds half the divisor, which is zero
        /// for a zero shift.
        /// </summary>
        public int Bias { get; }
    }

    /// <summary>
    /// Widens eight-bit samples and applies the shared temporal filter lane arithmetic.
    /// </summary>
    internal readonly partial struct ByteOperator : ITemporalFilterOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSquaredErrors(ref byte frame, ref byte prediction, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SquaredErrors(TemporalFilterLanes.LoadBytes(ref frame, lanes), TemporalFilterLanes.LoadBytes(ref prediction, lanes)).StoreUnsafe(ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSquaredErrors(ref byte frame, ref byte prediction, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SquaredErrors(TemporalFilterLanes.LoadBytes(ref frame, lanes), TemporalFilterLanes.LoadBytes(ref prediction, lanes)).StoreUnsafe(ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSquaredErrors(ref byte frame, ref byte prediction, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SquaredErrors(TemporalFilterLanes.LoadBytes(ref frame, lanes), TemporalFilterLanes.LoadBytes(ref prediction, lanes)).StoreUnsafe(ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSquaredErrors(byte frame, byte prediction, ref uint destination)
            => destination = TemporalFilterLanes.SquaredError(frame, prediction);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(ref byte sample, ref uint accumulator, ref ushort count, Vector128<uint> lanes)
            => TemporalFilterLanes.AccumulateSelf(TemporalFilterLanes.LoadBytes(ref sample, lanes), ref accumulator, ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(ref byte sample, ref uint accumulator, ref ushort count, Vector256<uint> lanes)
            => TemporalFilterLanes.AccumulateSelf(TemporalFilterLanes.LoadBytes(ref sample, lanes), ref accumulator, ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(ref byte sample, ref uint accumulator, ref ushort count, Vector512<uint> lanes)
            => TemporalFilterLanes.AccumulateSelf(TemporalFilterLanes.LoadBytes(ref sample, lanes), ref accumulator, ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(byte sample, ref uint accumulator, ref ushort count)
            => TemporalFilterLanes.AccumulateSelf(sample, ref accumulator, ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(ref uint windowErrors, ref byte prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector128<double> lanes)
            => TemporalFilterLanes.AccumulateWeights(Vector128.LoadUnsafe(ref windowErrors), TemporalFilterLanes.LoadBytes(ref prediction, default(Vector128<uint>)), ref accumulator, ref count, in terms, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(ref uint windowErrors, ref byte prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector256<double> lanes)
            => TemporalFilterLanes.AccumulateWeights(Vector128.LoadUnsafe(ref windowErrors), TemporalFilterLanes.LoadBytes(ref prediction, default(Vector128<uint>)), ref accumulator, ref count, in terms, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(ref uint windowErrors, ref byte prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector512<double> lanes)
            => TemporalFilterLanes.AccumulateWeights(Vector256.LoadUnsafe(ref windowErrors), TemporalFilterLanes.LoadBytes(ref prediction, default(Vector256<uint>)), ref accumulator, ref count, in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(uint windowError, byte prediction, ref uint accumulator, ref ushort count, in WeightTerms terms)
            => TemporalFilterLanes.AccumulateWeights(windowError, prediction, ref accumulator, ref count, in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(ref uint accumulator, ref ushort count, ref byte destination, Vector128<double> lanes)
            => TemporalFilterLanes.StoreBytes(TemporalFilterLanes.Normalize(ref accumulator, ref count, lanes), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(ref uint accumulator, ref ushort count, ref byte destination, Vector256<double> lanes)
            => TemporalFilterLanes.StoreBytes(TemporalFilterLanes.Normalize(ref accumulator, ref count, lanes), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(ref uint accumulator, ref ushort count, ref byte destination, Vector512<double> lanes)
            => TemporalFilterLanes.StoreBytes(TemporalFilterLanes.Normalize(ref accumulator, ref count), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(uint accumulator, ushort count, ref byte destination)
            => destination = (byte)TemporalFilterLanes.Normalize(accumulator, count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(ref byte center, int stride, in NoiseTerms terms, ref Vector128<int> sum, ref Vector128<int> count)
        {
            // Nine neighborhoods are read as eight-byte rows around the center and widened to sixteen-bit lanes.
            Vector128<short> a = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride - 1), default(Vector128<short>));
            Vector128<short> b = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride), default(Vector128<short>));
            Vector128<short> c = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride + 1), default(Vector128<short>));
            Vector128<short> d = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -1), default(Vector128<short>));
            Vector128<short> e = TemporalFilterLanes.LoadBytes(ref center, default(Vector128<short>));
            Vector128<short> f = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, 1), default(Vector128<short>));
            Vector128<short> g = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride - 1), default(Vector128<short>));
            Vector128<short> h = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride), default(Vector128<short>));
            Vector128<short> i = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride + 1), default(Vector128<short>));
            TemporalFilterLanes.AccumulateNoise(a, b, c, d, e, f, g, h, i, in terms, ref sum, ref count);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(ref byte center, int stride, in NoiseTerms terms, ref Vector256<int> sum, ref Vector256<int> count)
        {
            Vector256<short> a = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride - 1), default(Vector256<short>));
            Vector256<short> b = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride), default(Vector256<short>));
            Vector256<short> c = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride + 1), default(Vector256<short>));
            Vector256<short> d = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -1), default(Vector256<short>));
            Vector256<short> e = TemporalFilterLanes.LoadBytes(ref center, default(Vector256<short>));
            Vector256<short> f = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, 1), default(Vector256<short>));
            Vector256<short> g = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride - 1), default(Vector256<short>));
            Vector256<short> h = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride), default(Vector256<short>));
            Vector256<short> i = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride + 1), default(Vector256<short>));
            TemporalFilterLanes.AccumulateNoise(a, b, c, d, e, f, g, h, i, in terms, ref sum, ref count);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(ref byte center, int stride, in NoiseTerms terms, ref Vector512<int> sum, ref Vector512<int> count)
        {
            Vector512<short> a = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride - 1), default(Vector512<short>));
            Vector512<short> b = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride), default(Vector512<short>));
            Vector512<short> c = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -stride + 1), default(Vector512<short>));
            Vector512<short> d = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, -1), default(Vector512<short>));
            Vector512<short> e = TemporalFilterLanes.LoadBytes(ref center, default(Vector512<short>));
            Vector512<short> f = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, 1), default(Vector512<short>));
            Vector512<short> g = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride - 1), default(Vector512<short>));
            Vector512<short> h = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride), default(Vector512<short>));
            Vector512<short> i = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref center, stride + 1), default(Vector512<short>));
            TemporalFilterLanes.AccumulateNoise(a, b, c, d, e, f, g, h, i, in terms, ref sum, ref count);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(ref byte center, int stride, in NoiseTerms terms, ref int sum, ref int count)
            => TemporalFilterLanes.AccumulateNoise(
                Unsafe.Add(ref center, -stride - 1),
                Unsafe.Add(ref center, -stride),
                Unsafe.Add(ref center, -stride + 1),
                Unsafe.Add(ref center, -1),
                center,
                Unsafe.Add(ref center, 1),
                Unsafe.Add(ref center, stride - 1),
                Unsafe.Add(ref center, stride),
                Unsafe.Add(ref center, stride + 1),
                in terms,
                ref sum,
                ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SumRows(ref row0, ref row1, ref row2, ref row3, ref row4, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SumRows(ref row0, ref row1, ref row2, ref row3, ref row4, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SumRows(ref row0, ref row1, ref row2, ref row3, ref row4, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(uint row0, uint row1, uint row2, uint row3, uint row4, ref uint destination)
            => destination = row0 + row1 + row2 + row3 + row4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SumWindow(ref columns, ref luma, shift, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SumWindow(ref columns, ref luma, shift, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SumWindow(ref columns, ref luma, shift, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, uint luma, int shift, ref uint destination)
            => TemporalFilterLanes.SumWindow(ref columns, luma, shift, ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SumLumaPairs(ref row, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SumLumaPairs(ref row, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SumLumaPairs(ref row, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination)
            => destination = row + Unsafe.Add(ref row, 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SumLumaQuads(ref upper, ref lower, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SumLumaQuads(ref upper, ref lower, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SumLumaQuads(ref upper, ref lower, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination)
            => destination = upper + Unsafe.Add(ref upper, 1) + lower + Unsafe.Add(ref lower, 1);
    }

    /// <summary>
    /// Widens high-bit-depth samples and applies the shared temporal filter lane arithmetic.
    /// </summary>
    internal readonly partial struct UInt16Operator : ITemporalFilterOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSquaredErrors(ref ushort frame, ref ushort prediction, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SquaredErrors(TemporalFilterLanes.LoadWords(ref frame, lanes), TemporalFilterLanes.LoadWords(ref prediction, lanes)).StoreUnsafe(ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSquaredErrors(ref ushort frame, ref ushort prediction, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SquaredErrors(TemporalFilterLanes.LoadWords(ref frame, lanes), TemporalFilterLanes.LoadWords(ref prediction, lanes)).StoreUnsafe(ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSquaredErrors(ref ushort frame, ref ushort prediction, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SquaredErrors(TemporalFilterLanes.LoadWords(ref frame, lanes), TemporalFilterLanes.LoadWords(ref prediction, lanes)).StoreUnsafe(ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreSquaredErrors(ushort frame, ushort prediction, ref uint destination)
            => destination = TemporalFilterLanes.SquaredError(frame, prediction);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(ref ushort sample, ref uint accumulator, ref ushort count, Vector128<uint> lanes)
            => TemporalFilterLanes.AccumulateSelf(TemporalFilterLanes.LoadWords(ref sample, lanes), ref accumulator, ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(ref ushort sample, ref uint accumulator, ref ushort count, Vector256<uint> lanes)
            => TemporalFilterLanes.AccumulateSelf(TemporalFilterLanes.LoadWords(ref sample, lanes), ref accumulator, ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(ref ushort sample, ref uint accumulator, ref ushort count, Vector512<uint> lanes)
            => TemporalFilterLanes.AccumulateSelf(TemporalFilterLanes.LoadWords(ref sample, lanes), ref accumulator, ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(ushort sample, ref uint accumulator, ref ushort count)
            => TemporalFilterLanes.AccumulateSelf(sample, ref accumulator, ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(ref uint windowErrors, ref ushort prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector128<double> lanes)
            => TemporalFilterLanes.AccumulateWeights(Vector128.LoadUnsafe(ref windowErrors), TemporalFilterLanes.LoadWords(ref prediction, default(Vector128<uint>)), ref accumulator, ref count, in terms, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(ref uint windowErrors, ref ushort prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector256<double> lanes)
            => TemporalFilterLanes.AccumulateWeights(Vector128.LoadUnsafe(ref windowErrors), TemporalFilterLanes.LoadWords(ref prediction, default(Vector128<uint>)), ref accumulator, ref count, in terms, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(ref uint windowErrors, ref ushort prediction, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector512<double> lanes)
            => TemporalFilterLanes.AccumulateWeights(Vector256.LoadUnsafe(ref windowErrors), TemporalFilterLanes.LoadWords(ref prediction, default(Vector256<uint>)), ref accumulator, ref count, in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(uint windowError, ushort prediction, ref uint accumulator, ref ushort count, in WeightTerms terms)
            => TemporalFilterLanes.AccumulateWeights(windowError, prediction, ref accumulator, ref count, in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(ref uint accumulator, ref ushort count, ref ushort destination, Vector128<double> lanes)
            => TemporalFilterLanes.StoreWords(TemporalFilterLanes.Normalize(ref accumulator, ref count, lanes), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(ref uint accumulator, ref ushort count, ref ushort destination, Vector256<double> lanes)
            => TemporalFilterLanes.StoreWords(TemporalFilterLanes.Normalize(ref accumulator, ref count, lanes), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(ref uint accumulator, ref ushort count, ref ushort destination, Vector512<double> lanes)
            => TemporalFilterLanes.StoreWords(TemporalFilterLanes.Normalize(ref accumulator, ref count), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(uint accumulator, ushort count, ref ushort destination)
            => destination = (ushort)TemporalFilterLanes.Normalize(accumulator, count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(ref ushort center, int stride, in NoiseTerms terms, ref Vector128<int> sum, ref Vector128<int> count)
        {
            // Twelve-bit samples fit the signed sixteen-bit lanes directly; see TemporalFilterLanes.AccumulateNoise().
            Vector128<short> a = Vector128.LoadUnsafe(ref Unsafe.Add(ref center, -stride - 1)).AsInt16();
            Vector128<short> b = Vector128.LoadUnsafe(ref Unsafe.Add(ref center, -stride)).AsInt16();
            Vector128<short> c = Vector128.LoadUnsafe(ref Unsafe.Add(ref center, -stride + 1)).AsInt16();
            Vector128<short> d = Vector128.LoadUnsafe(ref Unsafe.Add(ref center, -1)).AsInt16();
            Vector128<short> e = Vector128.LoadUnsafe(ref center).AsInt16();
            Vector128<short> f = Vector128.LoadUnsafe(ref Unsafe.Add(ref center, 1)).AsInt16();
            Vector128<short> g = Vector128.LoadUnsafe(ref Unsafe.Add(ref center, stride - 1)).AsInt16();
            Vector128<short> h = Vector128.LoadUnsafe(ref Unsafe.Add(ref center, stride)).AsInt16();
            Vector128<short> i = Vector128.LoadUnsafe(ref Unsafe.Add(ref center, stride + 1)).AsInt16();
            TemporalFilterLanes.AccumulateNoise(a, b, c, d, e, f, g, h, i, in terms, ref sum, ref count);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(ref ushort center, int stride, in NoiseTerms terms, ref Vector256<int> sum, ref Vector256<int> count)
        {
            Vector256<short> a = Vector256.LoadUnsafe(ref Unsafe.Add(ref center, -stride - 1)).AsInt16();
            Vector256<short> b = Vector256.LoadUnsafe(ref Unsafe.Add(ref center, -stride)).AsInt16();
            Vector256<short> c = Vector256.LoadUnsafe(ref Unsafe.Add(ref center, -stride + 1)).AsInt16();
            Vector256<short> d = Vector256.LoadUnsafe(ref Unsafe.Add(ref center, -1)).AsInt16();
            Vector256<short> e = Vector256.LoadUnsafe(ref center).AsInt16();
            Vector256<short> f = Vector256.LoadUnsafe(ref Unsafe.Add(ref center, 1)).AsInt16();
            Vector256<short> g = Vector256.LoadUnsafe(ref Unsafe.Add(ref center, stride - 1)).AsInt16();
            Vector256<short> h = Vector256.LoadUnsafe(ref Unsafe.Add(ref center, stride)).AsInt16();
            Vector256<short> i = Vector256.LoadUnsafe(ref Unsafe.Add(ref center, stride + 1)).AsInt16();
            TemporalFilterLanes.AccumulateNoise(a, b, c, d, e, f, g, h, i, in terms, ref sum, ref count);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(ref ushort center, int stride, in NoiseTerms terms, ref Vector512<int> sum, ref Vector512<int> count)
        {
            Vector512<short> a = Vector512.LoadUnsafe(ref Unsafe.Add(ref center, -stride - 1)).AsInt16();
            Vector512<short> b = Vector512.LoadUnsafe(ref Unsafe.Add(ref center, -stride)).AsInt16();
            Vector512<short> c = Vector512.LoadUnsafe(ref Unsafe.Add(ref center, -stride + 1)).AsInt16();
            Vector512<short> d = Vector512.LoadUnsafe(ref Unsafe.Add(ref center, -1)).AsInt16();
            Vector512<short> e = Vector512.LoadUnsafe(ref center).AsInt16();
            Vector512<short> f = Vector512.LoadUnsafe(ref Unsafe.Add(ref center, 1)).AsInt16();
            Vector512<short> g = Vector512.LoadUnsafe(ref Unsafe.Add(ref center, stride - 1)).AsInt16();
            Vector512<short> h = Vector512.LoadUnsafe(ref Unsafe.Add(ref center, stride)).AsInt16();
            Vector512<short> i = Vector512.LoadUnsafe(ref Unsafe.Add(ref center, stride + 1)).AsInt16();
            TemporalFilterLanes.AccumulateNoise(a, b, c, d, e, f, g, h, i, in terms, ref sum, ref count);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(ref ushort center, int stride, in NoiseTerms terms, ref int sum, ref int count)
            => TemporalFilterLanes.AccumulateNoise(
                Unsafe.Add(ref center, -stride - 1),
                Unsafe.Add(ref center, -stride),
                Unsafe.Add(ref center, -stride + 1),
                Unsafe.Add(ref center, -1),
                center,
                Unsafe.Add(ref center, 1),
                Unsafe.Add(ref center, stride - 1),
                Unsafe.Add(ref center, stride),
                Unsafe.Add(ref center, stride + 1),
                in terms,
                ref sum,
                ref count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SumRows(ref row0, ref row1, ref row2, ref row3, ref row4, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SumRows(ref row0, ref row1, ref row2, ref row3, ref row4, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SumRows(ref row0, ref row1, ref row2, ref row3, ref row4, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(uint row0, uint row1, uint row2, uint row3, uint row4, ref uint destination)
            => destination = row0 + row1 + row2 + row3 + row4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SumWindow(ref columns, ref luma, shift, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SumWindow(ref columns, ref luma, shift, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SumWindow(ref columns, ref luma, shift, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, uint luma, int shift, ref uint destination)
            => TemporalFilterLanes.SumWindow(ref columns, luma, shift, ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SumLumaPairs(ref row, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SumLumaPairs(ref row, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SumLumaPairs(ref row, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination)
            => destination = row + Unsafe.Add(ref row, 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector128<uint> lanes)
            => TemporalFilterLanes.SumLumaQuads(ref upper, ref lower, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector256<uint> lanes)
            => TemporalFilterLanes.SumLumaQuads(ref upper, ref lower, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector512<uint> lanes)
            => TemporalFilterLanes.SumLumaQuads(ref upper, ref lower, ref destination, lanes);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination)
            => destination = upper + Unsafe.Add(ref upper, 1) + lower + Unsafe.Add(ref lower, 1);
    }
}
