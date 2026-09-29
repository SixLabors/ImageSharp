// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <content>
/// Builds the temporal filter predictions with the twelve-tap MULTITAP_SHARP2 interpolation filter.
/// </content>
internal static partial class Av1TemporalFilter
{
    /// <summary>
    /// The number of taps of the MULTITAP_SHARP2 filter.
    /// </summary>
    private const int SharpTaps = 12;

    /// <summary>
    /// The number of samples the filter reads before the output position, taps / 2 - 1.
    /// </summary>
    private const int SharpTapOffset = (SharpTaps / 2) - 1;

    /// <summary>
    /// The largest number of intermediate values of one sub-block: a sixteen-sample row for sixteen output rows
    /// plus the eleven rows the vertical taps add.
    /// </summary>
    internal const int PredictionIntermediateLength = 16 * (16 + SharpTaps - 1);

    /// <summary>
    /// Defines the twelve-tap single-reference convolution for one sample storage type across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <remarks>
    /// One lane is one output sample in a thirty-two-bit sum. Every kernel multiplies twelve loads by broadcast
    /// coefficients, the same exact integer sums that av1_convolve_x_sr_avx2(), av1_convolve_y_sr_avx2(),
    /// av1_convolve_2d_sr_avx2() and the SSSE3 high-bit-depth twelve-tap kernels form with _mm_madd_epi16().
    /// </remarks>
    internal interface ISharpPredictionOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Filters four samples horizontally and stores them. Reference: av1_convolve_x_sr_avx2() and
        /// av1_highbd_convolve_x_sr_ssse3() with twelve taps.
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterHorizontal(ref TSample source, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector128<int> lanes);

        /// <summary>
        /// Filters eight samples horizontally and stores them. Reference: av1_convolve_x_sr_avx2() and
        /// av1_highbd_convolve_x_sr_ssse3() with twelve taps.
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterHorizontal(ref TSample source, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector256<int> lanes);

        /// <summary>
        /// Filters sixteen samples horizontally and stores them. Reference: av1_convolve_x_sr_avx2() and
        /// av1_highbd_convolve_x_sr_ssse3() with twelve taps.
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterHorizontal(ref TSample source, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector512<int> lanes);

        /// <summary>
        /// Filters one sample horizontally and stores it. Reference: av1_convolve_x_sr_c() and
        /// av1_highbd_convolve_x_sr_c().
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The output sample.</param>
        public static abstract void FilterHorizontal(ref TSample source, ref short kernel, in ConvolveTerms terms, ref TSample destination);

        /// <summary>
        /// Filters four samples vertically and stores them. Reference: av1_convolve_y_sr_avx2() and
        /// av1_highbd_convolve_y_sr_ssse3() with twelve taps.
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="stride">The source row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterVertical(ref TSample source, int stride, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector128<int> lanes);

        /// <summary>
        /// Filters eight samples vertically and stores them. Reference: av1_convolve_y_sr_avx2() and
        /// av1_highbd_convolve_y_sr_ssse3() with twelve taps.
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="stride">The source row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterVertical(ref TSample source, int stride, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector256<int> lanes);

        /// <summary>
        /// Filters sixteen samples vertically and stores them. Reference: av1_convolve_y_sr_avx2() and
        /// av1_highbd_convolve_y_sr_ssse3() with twelve taps.
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="stride">The source row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterVertical(ref TSample source, int stride, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector512<int> lanes);

        /// <summary>
        /// Filters one sample vertically and stores it. Reference: av1_convolve_y_sr_c() and
        /// av1_highbd_convolve_y_sr_c().
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="stride">The source row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The output sample.</param>
        public static abstract void FilterVertical(ref TSample source, int stride, ref short kernel, in ConvolveTerms terms, ref TSample destination);

        /// <summary>
        /// Filters four samples horizontally into the signed sixteen-bit intermediate of the two-dimensional filter.
        /// Reference: the horizontal pass of av1_convolve_2d_sr_avx2() and av1_highbd_convolve_2d_sr_ssse3().
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first intermediate value.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterIntermediate(ref TSample source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector128<int> lanes);

        /// <summary>
        /// Filters eight samples horizontally into the signed sixteen-bit intermediate of the two-dimensional filter.
        /// Reference: the horizontal pass of av1_convolve_2d_sr_avx2() and av1_highbd_convolve_2d_sr_ssse3().
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first intermediate value.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterIntermediate(ref TSample source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector256<int> lanes);

        /// <summary>
        /// Filters sixteen samples horizontally into the signed sixteen-bit intermediate of the two-dimensional
        /// filter. Reference: the horizontal pass of av1_convolve_2d_sr_avx2() and av1_highbd_convolve_2d_sr_ssse3().
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first intermediate value.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterIntermediate(ref TSample source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector512<int> lanes);

        /// <summary>
        /// Filters one sample horizontally into the signed sixteen-bit intermediate of the two-dimensional filter.
        /// Reference: the horizontal pass of av1_convolve_2d_sr_c() and av1_highbd_convolve_2d_sr_c().
        /// </summary>
        /// <param name="source">The source sample of the first tap.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The intermediate value.</param>
        public static abstract void FilterIntermediate(ref TSample source, ref short kernel, in ConvolveTerms terms, ref short destination);

        /// <summary>
        /// Filters four intermediate values vertically and stores the samples. Reference: the vertical pass of
        /// av1_convolve_2d_sr_avx2() and av1_highbd_convolve_2d_sr_ssse3().
        /// </summary>
        /// <param name="source">The intermediate value of the first tap.</param>
        /// <param name="stride">The intermediate row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector128<int> lanes);

        /// <summary>
        /// Filters eight intermediate values vertically and stores the samples. Reference: the vertical pass of
        /// av1_convolve_2d_sr_avx2() and av1_highbd_convolve_2d_sr_ssse3().
        /// </summary>
        /// <param name="source">The intermediate value of the first tap.</param>
        /// <param name="stride">The intermediate row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector256<int> lanes);

        /// <summary>
        /// Filters sixteen intermediate values vertically and stores the samples. Reference: the vertical pass of
        /// av1_convolve_2d_sr_avx2() and av1_highbd_convolve_2d_sr_ssse3().
        /// </summary>
        /// <param name="source">The intermediate value of the first tap.</param>
        /// <param name="stride">The intermediate row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The first output sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        public static abstract void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref TSample destination, Vector512<int> lanes);

        /// <summary>
        /// Filters one intermediate value vertically and stores the sample. Reference: the vertical pass of
        /// av1_convolve_2d_sr_c() and av1_highbd_convolve_2d_sr_c().
        /// </summary>
        /// <param name="source">The intermediate value of the first tap.</param>
        /// <param name="stride">The intermediate row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <param name="destination">The output sample.</param>
        public static abstract void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref TSample destination);
    }

    /// <summary>
    /// Gets the twelve-tap kernels of MULTITAP_SHARP2 for the sixteen subpixel phases.
    /// Reference: av1_sub_pel_filters_12sharp.
    /// </summary>
    private static ReadOnlySpan<short> SharpTwelveTap =>
    [
        0, 0, 0, 0, 0, 128, 0, 0, 0, 0, 0, 0,
        0, 1, -2, 3, -7, 127, 8, -4, 2, -1, 1, 0,
        -1, 2, -3, 6, -13, 124, 18, -8, 4, -2, 2, -1,
        -1, 3, -4, 8, -18, 120, 28, -12, 7, -4, 2, -1,
        -1, 3, -6, 10, -21, 115, 38, -15, 8, -5, 3, -1,
        -2, 4, -6, 12, -24, 108, 49, -18, 10, -6, 3, -2,
        -2, 4, -7, 13, -25, 100, 60, -21, 11, -7, 4, -2,
        -2, 4, -7, 13, -26, 91, 71, -24, 13, -7, 4, -2,
        -2, 4, -7, 13, -25, 81, 81, -25, 13, -7, 4, -2,
        -2, 4, -7, 13, -24, 71, 91, -26, 13, -7, 4, -2,
        -2, 4, -7, 11, -21, 60, 100, -25, 13, -7, 4, -2,
        -2, 3, -6, 10, -18, 49, 108, -24, 12, -6, 4, -2,
        -1, 3, -5, 8, -15, 38, 115, -21, 10, -6, 3, -1,
        -1, 2, -4, 7, -12, 28, 120, -18, 8, -4, 3, -1,
        -1, 2, -2, 4, -8, 18, 124, -13, 6, -3, 2, -1,
        0, 1, -1, 2, -4, 8, 127, -7, 3, -2, 1, 0,
    ];

    /// <summary>
    /// Builds the predictions of one 64x64 filter block from one reference frame, sub-block by sub-block with the
    /// sub-block motion vectors. Reference: tf_build_predictor().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="reference">The reference frame.</param>
    /// <param name="blockRow">The filter block row.</param>
    /// <param name="blockColumn">The filter block column.</param>
    /// <param name="subblockVectors">The sixteen sub-block motion vectors in eighth-sample luma units.</param>
    /// <param name="intermediate">Scratch for the two-dimensional intermediate, <see cref="PredictionIntermediateLength"/> values.</param>
    /// <param name="prediction">The prediction of all planes, each packed at its block width, in plane order.</param>
    internal static void BuildPredictor<TSample, TOperator>(
        Av1EncoderFrame<TSample> reference,
        int blockRow,
        int blockColumn,
        ReadOnlySpan<Av1MotionVector> subblockVectors,
        Span<short> intermediate,
        Span<TSample> prediction)
        where TSample : unmanaged
        where TOperator : struct, ISharpPredictionOperator<TSample>
    {
        ConvolveTerms terms = new(reference.LumaBitDepth);
        int planeCount = reference.IsMonochrome ? 1 : 3;
        int planeOffset = 0;
        for (int plane = 0; plane < planeCount; plane++)
        {
            int subsamplingX = plane == 0 ? 0 : reference.ChromaSubsamplingX;
            int subsamplingY = plane == 0 ? 0 : reference.ChromaSubsamplingY;
            int planeHeight = BlockSize >> subsamplingY;
            int planeWidth = BlockSize >> subsamplingX;
            int planeY = (BlockSize * blockRow) >> subsamplingY;
            int planeX = (BlockSize * blockColumn) >> subsamplingX;
            int halfHeight = planeHeight >> 1;
            int halfWidth = planeWidth >> 1;
            int subblockHeight = planeHeight >> 2;
            int subblockWidth = planeWidth >> 2;

            // The reference buffer of av1_init_inter_params() spans the coded plane: libaom's widths[] and heights[]
            // are the dimensions aligned to eight luma samples, shifted for chroma.
            Buffer2DRegion<TSample> region = reference.CodedView.GetPlane((Av1Plane)plane);
            ReadOnlySpan<TSample> samples = region.Buffer.DangerousGetSingleSpan();
            int stride = region.Buffer.Width;
            int origin = (region.Bounds.Y * stride) + region.Bounds.X;
            Span<TSample> planePrediction = prediction.Slice(planeOffset, planeWidth * planeHeight);
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                int quadrantY = (quadrant >> 1) * halfHeight;
                int quadrantX = (quadrant & 1) * halfWidth;
                int subblock = quadrant * 4;
                for (int i = 0; i < halfHeight; i += subblockHeight)
                {
                    for (int j = 0; j < halfWidth; j += subblockWidth)
                    {
                        PredictSubblock<TSample, TOperator>(
                            samples,
                            stride,
                            origin,
                            region.Bounds.Width,
                            region.Bounds.Height,
                            planeY + quadrantY + i,
                            planeX + quadrantX + j,
                            subsamplingX,
                            subsamplingY,
                            subblockVectors[subblock++],
                            subblockWidth,
                            subblockHeight,
                            in terms,
                            intermediate,
                            planePrediction[(((quadrantY + i) * planeWidth) + quadrantX + j)..],
                            planeWidth);
                    }
                }
            }

            planeOffset += planeHeight * planeWidth;
        }
    }

    /// <summary>
    /// Predicts one sub-block of one plane with the twelve-tap MULTITAP_SHARP2 filter. Reference:
    /// av1_enc_build_one_inter_predictor(), with the position arithmetic of init_subpel_params() and the dispatch
    /// of convolve_2d_facade_single() and highbd_convolve_2d_facade_single().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="samples">The complete bordered reference plane.</param>
    /// <param name="stride">The reference row stride.</param>
    /// <param name="origin">The index of the top-left coded sample.</param>
    /// <param name="planeWidth">The coded plane width.</param>
    /// <param name="planeHeight">The coded plane height.</param>
    /// <param name="y">The sub-block row in the plane.</param>
    /// <param name="x">The sub-block column in the plane.</param>
    /// <param name="subsamplingX">The plane's horizontal subsampling shift.</param>
    /// <param name="subsamplingY">The plane's vertical subsampling shift.</param>
    /// <param name="vector">The motion vector in eighth-sample luma units.</param>
    /// <param name="width">The sub-block width.</param>
    /// <param name="height">The sub-block height.</param>
    /// <param name="terms">The rounding of the sample precision.</param>
    /// <param name="intermediate">Scratch for the two-dimensional intermediate.</param>
    /// <param name="destination">The first predicted sample.</param>
    /// <param name="destinationStride">The prediction row stride.</param>
    internal static void PredictSubblock<TSample, TOperator>(
        ReadOnlySpan<TSample> samples,
        int stride,
        int origin,
        int planeWidth,
        int planeHeight,
        int y,
        int x,
        int subsamplingX,
        int subsamplingY,
        Av1MotionVector vector,
        int width,
        int height,
        in ConvolveTerms terms,
        Span<short> intermediate,
        Span<TSample> destination,
        int destinationStride)
        where TSample : unmanaged
        where TOperator : struct, ISharpPredictionOperator<TSample>
    {
        // Positions are in 1/1024 sample units: the motion vector in 1/16 plane samples, raised by the six
        // SCALE_EXTRA_BITS, plus the half-unit SCALE_EXTRA_OFF. Each axis is then clamped to the region that
        // av1_init_inter_params() allows: AOM_BORDER_IN_PIXELS minus AOM_INTERP_EXTEND before the plane and
        // AOM_INTERP_EXTEND after the coded plane. A clamped position is a whole sample.
        const int ScaleSubpixelBits = 10;
        const int BorderInPixels = 288;
        const int InterpolationExtend = 4;
        int positionY = (((y << 4) + (vector.Row * (1 << (1 - subsamplingY)))) << 6) + 32;
        int positionX = (((x << 4) + (vector.Column * (1 << (1 - subsamplingX)))) << 6) + 32;
        positionY = Math.Clamp(
            positionY,
            -(((BorderInPixels >> subsamplingY) - InterpolationExtend) << ScaleSubpixelBits),
            (planeHeight + InterpolationExtend) << ScaleSubpixelBits);

        positionX = Math.Clamp(
            positionX,
            -(((BorderInPixels >> subsamplingX) - InterpolationExtend) << ScaleSubpixelBits),
            (planeWidth + InterpolationExtend) << ScaleSubpixelBits);

        int phaseY = (positionY & ((1 << ScaleSubpixelBits) - 1)) >> 6;
        int phaseX = (positionX & ((1 << ScaleSubpixelBits) - 1)) >> 6;
        int start = origin + ((positionY >> ScaleSubpixelBits) * stride) + (positionX >> ScaleSubpixelBits);
        ref TSample destinationBase = ref MemoryMarshal.GetReference(destination[..(((height - 1) * destinationStride) + width)]);
        if (phaseX == 0 && phaseY == 0)
        {
            // aom_convolve_copy()
            for (int row = 0; row < height; row++)
            {
                samples.Slice(start + (row * stride), width).CopyTo(destination.Slice(row * destinationStride, width));
            }

            return;
        }

        ref short kernelX = ref MemoryMarshal.GetReference(SharpTwelveTap.Slice(phaseX * SharpTaps, SharpTaps));
        ref short kernelY = ref MemoryMarshal.GetReference(SharpTwelveTap.Slice(phaseY * SharpTaps, SharpTaps));
        if (phaseY == 0)
        {
            // av1_convolve_x_sr(): the taps start five samples left of each output.
            ref TSample sourceBase = ref MemoryMarshal.GetReference(
                samples.Slice(start - SharpTapOffset, ((height - 1) * stride) + width + SharpTaps - 1));

            for (int row = 0; row < height; row++)
            {
                ref TSample sourceRow = ref Unsafe.Add(ref sourceBase, row * stride);
                ref TSample destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                FilterHorizontalRow<TSample, TOperator>(ref sourceRow, ref kernelX, in terms, ref destinationRow, width);
            }

            return;
        }

        if (phaseX == 0)
        {
            // av1_convolve_y_sr(): the taps start five rows above each output.
            ref TSample sourceBase = ref MemoryMarshal.GetReference(
                samples.Slice(start - (SharpTapOffset * stride), ((height + SharpTaps - 2) * stride) + width));

            for (int row = 0; row < height; row++)
            {
                ref TSample sourceRow = ref Unsafe.Add(ref sourceBase, row * stride);
                ref TSample destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;
                if (Vector512.IsHardwareAccelerated)
                {
                    for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
                    {
                        TOperator.FilterVertical(
                            ref Unsafe.Add(ref sourceRow, column), stride, ref kernelY, in terms, ref Unsafe.Add(ref destinationRow, column), default(Vector512<int>));
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                    {
                        TOperator.FilterVertical(
                            ref Unsafe.Add(ref sourceRow, column), stride, ref kernelY, in terms, ref Unsafe.Add(ref destinationRow, column), default(Vector256<int>));
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                    {
                        TOperator.FilterVertical(
                            ref Unsafe.Add(ref sourceRow, column), stride, ref kernelY, in terms, ref Unsafe.Add(ref destinationRow, column), default(Vector128<int>));
                    }
                }

                for (; column < width; column++)
                {
                    TOperator.FilterVertical(ref Unsafe.Add(ref sourceRow, column), stride, ref kernelY, in terms, ref Unsafe.Add(ref destinationRow, column));
                }
            }

            return;
        }

        // av1_convolve_2d_sr(): the horizontal pass covers the eleven extra rows the vertical taps read.
        int intermediateHeight = height + SharpTaps - 1;
        ref short intermediateBase = ref MemoryMarshal.GetReference(intermediate[..(intermediateHeight * width)]);
        ref TSample source2dBase = ref MemoryMarshal.GetReference(
            samples.Slice(start - (SharpTapOffset * stride) - SharpTapOffset, ((intermediateHeight - 1) * stride) + width + SharpTaps - 1));

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref TSample sourceRow = ref Unsafe.Add(ref source2dBase, row * stride);
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * width);
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
                {
                    TOperator.FilterIntermediate(
                        ref Unsafe.Add(ref sourceRow, column), ref kernelX, in terms, ref Unsafe.Add(ref intermediateRow, column), default(Vector512<int>));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    TOperator.FilterIntermediate(
                        ref Unsafe.Add(ref sourceRow, column), ref kernelX, in terms, ref Unsafe.Add(ref intermediateRow, column), default(Vector256<int>));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    TOperator.FilterIntermediate(
                        ref Unsafe.Add(ref sourceRow, column), ref kernelX, in terms, ref Unsafe.Add(ref intermediateRow, column), default(Vector128<int>));
                }
            }

            for (; column < width; column++)
            {
                TOperator.FilterIntermediate(ref Unsafe.Add(ref sourceRow, column), ref kernelX, in terms, ref Unsafe.Add(ref intermediateRow, column));
            }
        }

        for (int row = 0; row < height; row++)
        {
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * width);
            ref TSample destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
                {
                    TOperator.FilterFinal(
                        ref Unsafe.Add(ref intermediateRow, column), width, ref kernelY, in terms, ref Unsafe.Add(ref destinationRow, column), default(Vector512<int>));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
                {
                    TOperator.FilterFinal(
                        ref Unsafe.Add(ref intermediateRow, column), width, ref kernelY, in terms, ref Unsafe.Add(ref destinationRow, column), default(Vector256<int>));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
                {
                    TOperator.FilterFinal(
                        ref Unsafe.Add(ref intermediateRow, column), width, ref kernelY, in terms, ref Unsafe.Add(ref destinationRow, column), default(Vector128<int>));
                }
            }

            for (; column < width; column++)
            {
                TOperator.FilterFinal(ref Unsafe.Add(ref intermediateRow, column), width, ref kernelY, in terms, ref Unsafe.Add(ref destinationRow, column));
            }
        }
    }

    /// <summary>
    /// Filters one row horizontally. Reference: the row loop of av1_convolve_x_sr_avx2().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="source">The source sample of the first tap of the first output.</param>
    /// <param name="kernel">The first of the twelve coefficients.</param>
    /// <param name="terms">The rounding of the sample precision.</param>
    /// <param name="destination">The first output sample.</param>
    /// <param name="width">The number of output samples.</param>
    private static void FilterHorizontalRow<TSample, TOperator>(ref TSample source, ref short kernel, in ConvolveTerms terms, ref TSample destination, int width)
        where TSample : unmanaged
        where TOperator : struct, ISharpPredictionOperator<TSample>
    {
        int column = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= width - Vector512<int>.Count; column += Vector512<int>.Count)
            {
                TOperator.FilterHorizontal(ref Unsafe.Add(ref source, column), ref kernel, in terms, ref Unsafe.Add(ref destination, column), default(Vector512<int>));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= width - Vector256<int>.Count; column += Vector256<int>.Count)
            {
                TOperator.FilterHorizontal(ref Unsafe.Add(ref source, column), ref kernel, in terms, ref Unsafe.Add(ref destination, column), default(Vector256<int>));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector128<int>.Count; column += Vector128<int>.Count)
            {
                TOperator.FilterHorizontal(ref Unsafe.Add(ref source, column), ref kernel, in terms, ref Unsafe.Add(ref destination, column), default(Vector128<int>));
            }
        }

        for (; column < width; column++)
        {
            TOperator.FilterHorizontal(ref Unsafe.Add(ref source, column), ref kernel, in terms, ref Unsafe.Add(ref destination, column));
        }
    }

    /// <summary>
    /// Carries the rounding constants of a single-reference convolution at one bit depth.
    /// Reference: get_conv_params() with is_compound 0.
    /// </summary>
    internal readonly struct ConvolveTerms
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConvolveTerms"/> struct. Reference: get_conv_params_no_round().
        /// </summary>
        /// <param name="bitDepth">The sample bit depth.</param>
        public ConvolveTerms(int bitDepth)
        {
            // get_conv_params_no_round() raises round_0 by two for twelve bits so that the intermediate stays inside
            // sixteen bits; round_1 completes the fourteen filter bits of the two passes.
            const int FilterBits = 7;
            this.Round0 = bitDepth == 12 ? 5 : 3;
            this.Round1 = (2 * FilterBits) - this.Round0;
            this.HorizontalBits = FilterBits - this.Round0;
            this.IntermediateOffset = 1 << (bitDepth + FilterBits - 1);
            this.FinalOffset = 1 << (bitDepth + (2 * FilterBits) - this.Round0);
            this.FinalSubtrahend = (1 << bitDepth) + (1 << (bitDepth - 1));
            this.Maximum = (1 << bitDepth) - 1;
        }

        /// <summary>
        /// Gets the rounding shift of the first pass, conv_params->round_0.
        /// </summary>
        public int Round0 { get; }

        /// <summary>
        /// Gets the rounding shift of the second pass, conv_params->round_1.
        /// </summary>
        public int Round1 { get; }

        /// <summary>
        /// Gets the second rounding shift of the horizontal-only filter, FILTER_BITS - round_0.
        /// </summary>
        public int HorizontalBits { get; }

        /// <summary>
        /// Gets the offset that keeps the two-dimensional intermediate nonnegative before rounding, 1 &lt;&lt; (bd + 6).
        /// </summary>
        public int IntermediateOffset { get; }

        /// <summary>
        /// Gets the offset of the two-dimensional vertical sum, 1 &lt;&lt; offset_bits.
        /// </summary>
        public int FinalOffset { get; }

        /// <summary>
        /// Gets the value that removes both offsets after the second rounding.
        /// </summary>
        public int FinalSubtrahend { get; }

        /// <summary>
        /// Gets the largest sample value.
        /// </summary>
        public int Maximum { get; }
    }

    /// <content>
    /// Implements the twelve-tap convolution for eight-bit samples.
    /// </content>
    internal readonly partial struct ByteOperator : ISharpPredictionOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterHorizontal(ref byte source, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector128<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundHorizontal(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterHorizontal(ref byte source, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector256<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundHorizontal(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterHorizontal(ref byte source, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector512<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundHorizontal(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterHorizontal(ref byte source, ref short kernel, in ConvolveTerms terms, ref byte destination)
            => destination = (byte)SharpFilterLanes.RoundHorizontal(SharpFilterLanes.Sum(ref source, 1, ref kernel), in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterVertical(ref byte source, int stride, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector128<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundVertical(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterVertical(ref byte source, int stride, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector256<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundVertical(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterVertical(ref byte source, int stride, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector512<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundVertical(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterVertical(ref byte source, int stride, ref short kernel, in ConvolveTerms terms, ref byte destination)
            => destination = (byte)SharpFilterLanes.RoundVertical(SharpFilterLanes.Sum(ref source, stride, ref kernel), in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterIntermediate(ref byte source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector128<int> lanes)
            => SharpFilterLanes.StoreShorts(SharpFilterLanes.RoundIntermediate(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterIntermediate(ref byte source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector256<int> lanes)
            => SharpFilterLanes.StoreShorts(SharpFilterLanes.RoundIntermediate(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterIntermediate(ref byte source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector512<int> lanes)
            => SharpFilterLanes.StoreShorts(SharpFilterLanes.RoundIntermediate(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterIntermediate(ref byte source, ref short kernel, in ConvolveTerms terms, ref short destination)
            => destination = (short)SharpFilterLanes.RoundIntermediate(SharpFilterLanes.Sum(ref source, 1, ref kernel), in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector128<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundFinal(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector256<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundFinal(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref byte destination, Vector512<int> lanes)
            => SharpFilterLanes.StoreBytes(SharpFilterLanes.RoundFinal(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref byte destination)
            => destination = (byte)SharpFilterLanes.RoundFinal(SharpFilterLanes.Sum(ref source, stride, ref kernel), in terms);
    }

    /// <content>
    /// Implements the twelve-tap convolution for high-bit-depth samples.
    /// </content>
    internal readonly partial struct UInt16Operator : ISharpPredictionOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterHorizontal(ref ushort source, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector128<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundHorizontal(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterHorizontal(ref ushort source, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector256<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundHorizontal(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterHorizontal(ref ushort source, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector512<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundHorizontal(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterHorizontal(ref ushort source, ref short kernel, in ConvolveTerms terms, ref ushort destination)
            => destination = (ushort)SharpFilterLanes.RoundHorizontal(SharpFilterLanes.Sum(ref source, 1, ref kernel), in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterVertical(ref ushort source, int stride, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector128<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundVertical(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterVertical(ref ushort source, int stride, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector256<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundVertical(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterVertical(ref ushort source, int stride, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector512<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundVertical(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterVertical(ref ushort source, int stride, ref short kernel, in ConvolveTerms terms, ref ushort destination)
            => destination = (ushort)SharpFilterLanes.RoundVertical(SharpFilterLanes.Sum(ref source, stride, ref kernel), in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterIntermediate(ref ushort source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector128<int> lanes)
            => SharpFilterLanes.StoreShorts(SharpFilterLanes.RoundIntermediate(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterIntermediate(ref ushort source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector256<int> lanes)
            => SharpFilterLanes.StoreShorts(SharpFilterLanes.RoundIntermediate(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterIntermediate(ref ushort source, ref short kernel, in ConvolveTerms terms, ref short destination, Vector512<int> lanes)
            => SharpFilterLanes.StoreShorts(SharpFilterLanes.RoundIntermediate(SharpFilterLanes.Sum(ref source, 1, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterIntermediate(ref ushort source, ref short kernel, in ConvolveTerms terms, ref short destination)
            => destination = (short)SharpFilterLanes.RoundIntermediate(SharpFilterLanes.Sum(ref source, 1, ref kernel), in terms);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector128<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundFinal(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector256<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundFinal(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref ushort destination, Vector512<int> lanes)
            => SharpFilterLanes.StoreWords(SharpFilterLanes.RoundFinal(SharpFilterLanes.Sum(ref source, stride, ref kernel, lanes), in terms), ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterFinal(ref short source, int stride, ref short kernel, in ConvolveTerms terms, ref ushort destination)
            => destination = (ushort)SharpFilterLanes.RoundFinal(SharpFilterLanes.Sum(ref source, stride, ref kernel), in terms);
    }

    /// <summary>
    /// Holds the lane arithmetic of the twelve-tap convolution, independent of the sample storage type.
    /// </summary>
    private static class SharpFilterLanes
    {
        /// <summary>
        /// Returns four twelve-tap sums of byte samples. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The sample of the first tap of the first output.</param>
        /// <param name="step">The distance between taps: one horizontally, the stride vertically.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Sum(ref byte source, int step, ref short kernel, Vector128<int> lanes)
        {
            Vector128<int> sum = Vector128<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector128<uint> taps = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref source, tap * step), default(Vector128<uint>));
                sum += taps.AsInt32() * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns eight twelve-tap sums of byte samples. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The sample of the first tap of the first output.</param>
        /// <param name="step">The distance between taps: one horizontally, the stride vertically.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Sum(ref byte source, int step, ref short kernel, Vector256<int> lanes)
        {
            Vector256<int> sum = Vector256<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector256<uint> taps = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref source, tap * step), default(Vector256<uint>));
                sum += taps.AsInt32() * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns sixteen twelve-tap sums of byte samples. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The sample of the first tap of the first output.</param>
        /// <param name="step">The distance between taps: one horizontally, the stride vertically.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Sum(ref byte source, int step, ref short kernel, Vector512<int> lanes)
        {
            Vector512<int> sum = Vector512<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector512<uint> taps = TemporalFilterLanes.LoadBytes(ref Unsafe.Add(ref source, tap * step), default(Vector512<uint>));
                sum += taps.AsInt32() * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns one twelve-tap sum of byte samples. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The sample of the first tap.</param>
        /// <param name="step">The distance between taps: one horizontally, the stride vertically.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <returns>The exact sum.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(ref byte source, int step, ref short kernel)
        {
            int sum = 0;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                sum += Unsafe.Add(ref kernel, tap) * Unsafe.Add(ref source, tap * step);
            }

            return sum;
        }

        /// <summary>
        /// Returns four twelve-tap sums of word samples. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The sample of the first tap of the first output.</param>
        /// <param name="step">The distance between taps: one horizontally, the stride vertically.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Sum(ref ushort source, int step, ref short kernel, Vector128<int> lanes)
        {
            // Twelve-bit samples and coefficients of at most 127 give sums below 2^22 in magnitude.
            Vector128<int> sum = Vector128<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector128<uint> taps = TemporalFilterLanes.LoadWords(ref Unsafe.Add(ref source, tap * step), default(Vector128<uint>));
                sum += taps.AsInt32() * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns eight twelve-tap sums of word samples. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The sample of the first tap of the first output.</param>
        /// <param name="step">The distance between taps: one horizontally, the stride vertically.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Sum(ref ushort source, int step, ref short kernel, Vector256<int> lanes)
        {
            Vector256<int> sum = Vector256<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector256<uint> taps = TemporalFilterLanes.LoadWords(ref Unsafe.Add(ref source, tap * step), default(Vector256<uint>));
                sum += taps.AsInt32() * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns sixteen twelve-tap sums of word samples. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The sample of the first tap of the first output.</param>
        /// <param name="step">The distance between taps: one horizontally, the stride vertically.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Sum(ref ushort source, int step, ref short kernel, Vector512<int> lanes)
        {
            Vector512<int> sum = Vector512<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector512<uint> taps = TemporalFilterLanes.LoadWords(ref Unsafe.Add(ref source, tap * step), default(Vector512<uint>));
                sum += taps.AsInt32() * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns one twelve-tap sum of word samples. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The sample of the first tap.</param>
        /// <param name="step">The distance between taps: one horizontally, the stride vertically.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <returns>The exact sum.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(ref ushort source, int step, ref short kernel)
        {
            int sum = 0;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                sum += Unsafe.Add(ref kernel, tap) * Unsafe.Add(ref source, tap * step);
            }

            return sum;
        }

        /// <summary>
        /// Returns four twelve-tap sums of intermediate values. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The intermediate value of the first tap of the first output.</param>
        /// <param name="step">The intermediate row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Sum(ref short source, int step, ref short kernel, Vector128<int> lanes)
        {
            // A saturated intermediate is at most 2^15 in magnitude and the absolute coefficients add to less than
            // 2^9, so the sums stay below 2^24 in magnitude.
            Vector128<int> sum = Vector128<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector128<short> values = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref source, tap * step)))).AsInt16();
                sum += Vector128.WidenLower(values) * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns eight twelve-tap sums of intermediate values. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The intermediate value of the first tap of the first output.</param>
        /// <param name="step">The intermediate row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Sum(ref short source, int step, ref short kernel, Vector256<int> lanes)
        {
            Vector256<int> sum = Vector256<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector256<int> values = Vector256.WidenLower(Vector128.LoadUnsafe(ref Unsafe.Add(ref source, tap * step)).ToVector256Unsafe());
                sum += values * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns sixteen twelve-tap sums of intermediate values. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The intermediate value of the first tap of the first output.</param>
        /// <param name="step">The intermediate row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The exact thirty-two-bit sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Sum(ref short source, int step, ref short kernel, Vector512<int> lanes)
        {
            Vector512<int> sum = Vector512<int>.Zero;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                Vector512<int> values = Vector512.WidenLower(Vector256.LoadUnsafe(ref Unsafe.Add(ref source, tap * step)).ToVector512Unsafe());
                sum += values * (int)Unsafe.Add(ref kernel, tap);
            }

            return sum;
        }

        /// <summary>
        /// Returns one twelve-tap sum of intermediate values. Reference: convolve_12taps().
        /// </summary>
        /// <param name="source">The intermediate value of the first tap.</param>
        /// <param name="step">The intermediate row stride.</param>
        /// <param name="kernel">The first of the twelve coefficients.</param>
        /// <returns>The exact sum.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(ref short source, int step, ref short kernel)
        {
            int sum = 0;
            for (int tap = 0; tap < SharpTaps; tap++)
            {
                sum += Unsafe.Add(ref kernel, tap) * Unsafe.Add(ref source, tap * step);
            }

            return sum;
        }

        /// <summary>
        /// Rounds horizontal-only sums to samples: round_0 and then FILTER_BITS - round_0, as two roundings. Reference:
        /// av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> RoundHorizontal(Vector128<int> sum, in ConvolveTerms terms)
        {
            Vector128<int> first = Vector128.ShiftRightArithmetic(sum + Vector128.Create(1 << (terms.Round0 - 1)), terms.Round0);
            Vector128<int> second = Vector128.ShiftRightArithmetic(first + Vector128.Create(1 << (terms.HorizontalBits - 1)), terms.HorizontalBits);
            return Vector128.Clamp(second, Vector128<int>.Zero, Vector128.Create(terms.Maximum));
        }

        /// <summary>
        /// Rounds horizontal-only sums to samples: round_0 and then FILTER_BITS - round_0, as two roundings. Reference:
        /// av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> RoundHorizontal(Vector256<int> sum, in ConvolveTerms terms)
        {
            Vector256<int> first = Vector256.ShiftRightArithmetic(sum + Vector256.Create(1 << (terms.Round0 - 1)), terms.Round0);
            Vector256<int> second = Vector256.ShiftRightArithmetic(first + Vector256.Create(1 << (terms.HorizontalBits - 1)), terms.HorizontalBits);
            return Vector256.Clamp(second, Vector256<int>.Zero, Vector256.Create(terms.Maximum));
        }

        /// <summary>
        /// Rounds horizontal-only sums to samples: round_0 and then FILTER_BITS - round_0, as two roundings. Reference:
        /// av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> RoundHorizontal(Vector512<int> sum, in ConvolveTerms terms)
        {
            Vector512<int> first = Vector512.ShiftRightArithmetic(sum + Vector512.Create(1 << (terms.Round0 - 1)), terms.Round0);
            Vector512<int> second = Vector512.ShiftRightArithmetic(first + Vector512.Create(1 << (terms.HorizontalBits - 1)), terms.HorizontalBits);
            return Vector512.Clamp(second, Vector512<int>.Zero, Vector512.Create(terms.Maximum));
        }

        /// <summary>
        /// Rounds one horizontal-only sum to a sample. Reference: av1_convolve_x_sr_c().
        /// </summary>
        /// <param name="sum">The twelve-tap sum.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped sample.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundHorizontal(int sum, in ConvolveTerms terms)
        {
            int first = (sum + (1 << (terms.Round0 - 1))) >> terms.Round0;
            return Math.Clamp((first + (1 << (terms.HorizontalBits - 1))) >> terms.HorizontalBits, 0, terms.Maximum);
        }

        /// <summary>
        /// Rounds vertical-only sums to samples with FILTER_BITS. Reference: av1_convolve_y_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> RoundVertical(Vector128<int> sum, in ConvolveTerms terms)
            => Vector128.Clamp(Vector128.ShiftRightArithmetic(sum + Vector128.Create(64), 7), Vector128<int>.Zero, Vector128.Create(terms.Maximum));

        /// <summary>
        /// Rounds vertical-only sums to samples with FILTER_BITS. Reference: av1_convolve_y_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> RoundVertical(Vector256<int> sum, in ConvolveTerms terms)
            => Vector256.Clamp(Vector256.ShiftRightArithmetic(sum + Vector256.Create(64), 7), Vector256<int>.Zero, Vector256.Create(terms.Maximum));

        /// <summary>
        /// Rounds vertical-only sums to samples with FILTER_BITS. Reference: av1_convolve_y_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> RoundVertical(Vector512<int> sum, in ConvolveTerms terms)
            => Vector512.Clamp(Vector512.ShiftRightArithmetic(sum + Vector512.Create(64), 7), Vector512<int>.Zero, Vector512.Create(terms.Maximum));

        /// <summary>
        /// Rounds one vertical-only sum to a sample. Reference: av1_convolve_y_sr_c().
        /// </summary>
        /// <param name="sum">The twelve-tap sum.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped sample.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundVertical(int sum, in ConvolveTerms terms)
            => Math.Clamp((sum + 64) >> 7, 0, terms.Maximum);

        /// <summary>
        /// Rounds horizontal sums of the two-dimensional filter to its intermediate, saturated to sixteen bits as the
        /// x64 kernels pack it. Reference: the horizontal pass of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The intermediate values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> RoundIntermediate(Vector128<int> sum, in ConvolveTerms terms)
        {
            Vector128<int> rounded = Vector128.ShiftRightArithmetic(sum + Vector128.Create(terms.IntermediateOffset + (1 << (terms.Round0 - 1))), terms.Round0);
            return Vector128.Clamp(rounded, Vector128.Create((int)short.MinValue), Vector128.Create((int)short.MaxValue));
        }

        /// <summary>
        /// Rounds horizontal sums of the two-dimensional filter to its intermediate, saturated to sixteen bits as the
        /// x64 kernels pack it. Reference: the horizontal pass of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The intermediate values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> RoundIntermediate(Vector256<int> sum, in ConvolveTerms terms)
        {
            Vector256<int> rounded = Vector256.ShiftRightArithmetic(sum + Vector256.Create(terms.IntermediateOffset + (1 << (terms.Round0 - 1))), terms.Round0);
            return Vector256.Clamp(rounded, Vector256.Create((int)short.MinValue), Vector256.Create((int)short.MaxValue));
        }

        /// <summary>
        /// Rounds horizontal sums of the two-dimensional filter to its intermediate, saturated to sixteen bits as the
        /// x64 kernels pack it. Reference: the horizontal pass of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The intermediate values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> RoundIntermediate(Vector512<int> sum, in ConvolveTerms terms)
        {
            Vector512<int> rounded = Vector512.ShiftRightArithmetic(sum + Vector512.Create(terms.IntermediateOffset + (1 << (terms.Round0 - 1))), terms.Round0);
            return Vector512.Clamp(rounded, Vector512.Create((int)short.MinValue), Vector512.Create((int)short.MaxValue));
        }

        /// <summary>
        /// Rounds one horizontal sum of the two-dimensional filter to its intermediate. Reference: av1_highbd_convolve_2d_sr_ssse3().
        /// </summary>
        /// <param name="sum">The twelve-tap sum.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The intermediate value.</returns>
        /// <remarks>
        /// av1_highbd_convolve_2d_sr_c() wraps an intermediate above 32767 when it stores it, while the x64 SSSE3
        /// kernel saturates it with _mm_packs_epi32(). Only high bit depth reaches that range, at the three central
        /// phases; the pinned x64 encoder saturates, so this port saturates as well.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundIntermediate(int sum, in ConvolveTerms terms)
            => Math.Clamp((sum + terms.IntermediateOffset + (1 << (terms.Round0 - 1))) >> terms.Round0, short.MinValue, short.MaxValue);

        /// <summary>
        /// Rounds vertical sums of the two-dimensional filter to samples: round_1, then both offsets removed. Reference:
        /// the vertical pass of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> RoundFinal(Vector128<int> sum, in ConvolveTerms terms)
        {
            Vector128<int> rounded = Vector128.ShiftRightArithmetic(sum + Vector128.Create(terms.FinalOffset + (1 << (terms.Round1 - 1))), terms.Round1);
            return Vector128.Clamp(rounded - Vector128.Create(terms.FinalSubtrahend), Vector128<int>.Zero, Vector128.Create(terms.Maximum));
        }

        /// <summary>
        /// Rounds vertical sums of the two-dimensional filter to samples: round_1, then both offsets removed. Reference:
        /// the vertical pass of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> RoundFinal(Vector256<int> sum, in ConvolveTerms terms)
        {
            Vector256<int> rounded = Vector256.ShiftRightArithmetic(sum + Vector256.Create(terms.FinalOffset + (1 << (terms.Round1 - 1))), terms.Round1);
            return Vector256.Clamp(rounded - Vector256.Create(terms.FinalSubtrahend), Vector256<int>.Zero, Vector256.Create(terms.Maximum));
        }

        /// <summary>
        /// Rounds vertical sums of the two-dimensional filter to samples: round_1, then both offsets removed. Reference:
        /// the vertical pass of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="sum">The twelve-tap sums.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> RoundFinal(Vector512<int> sum, in ConvolveTerms terms)
        {
            Vector512<int> rounded = Vector512.ShiftRightArithmetic(sum + Vector512.Create(terms.FinalOffset + (1 << (terms.Round1 - 1))), terms.Round1);
            return Vector512.Clamp(rounded - Vector512.Create(terms.FinalSubtrahend), Vector512<int>.Zero, Vector512.Create(terms.Maximum));
        }

        /// <summary>
        /// Rounds one vertical sum of the two-dimensional filter to a sample. Reference: av1_convolve_2d_sr_c().
        /// </summary>
        /// <param name="sum">The twelve-tap sum.</param>
        /// <param name="terms">The rounding of the sample precision.</param>
        /// <returns>The clipped sample.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundFinal(int sum, in ConvolveTerms terms)
            => Math.Clamp(((sum + terms.FinalOffset + (1 << (terms.Round1 - 1))) >> terms.Round1) - terms.FinalSubtrahend, 0, terms.Maximum);

        /// <summary>
        /// Stores four clipped samples as bytes. Reference: the packing stores of av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="destination">The first byte.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreBytes(Vector128<int> values, ref byte destination)
            => TemporalFilterLanes.StoreBytes(values.AsUInt32(), ref destination);

        /// <summary>
        /// Stores eight clipped samples as bytes. Reference: the packing stores of av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="destination">The first byte.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreBytes(Vector256<int> values, ref byte destination)
            => TemporalFilterLanes.StoreBytes(values.AsUInt32(), ref destination);

        /// <summary>
        /// Stores sixteen clipped samples as bytes. Reference: the packing stores of av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="destination">The first byte.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreBytes(Vector512<int> values, ref byte destination)
        {
            Vector256<ushort> words = Vector256.Narrow(values.GetLower().AsUInt32(), values.GetUpper().AsUInt32());
            Vector128.Narrow(words.GetLower(), words.GetUpper()).StoreUnsafe(ref destination);
        }

        /// <summary>
        /// Stores four clipped samples as words. Reference: the packing stores of av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="destination">The first word.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreWords(Vector128<int> values, ref ushort destination)
            => TemporalFilterLanes.StoreWords(values.AsUInt32(), ref destination);

        /// <summary>
        /// Stores eight clipped samples as words. Reference: the packing stores of av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="destination">The first word.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreWords(Vector256<int> values, ref ushort destination)
            => TemporalFilterLanes.StoreWords(values.AsUInt32(), ref destination);

        /// <summary>
        /// Stores sixteen clipped samples as words. Reference: the packing stores of av1_convolve_x_sr_avx2().
        /// </summary>
        /// <param name="values">The samples.</param>
        /// <param name="destination">The first word.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreWords(Vector512<int> values, ref ushort destination)
            => Vector256.Narrow(values.GetLower().AsUInt32(), values.GetUpper().AsUInt32()).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores four saturated intermediate values. Reference: the _mm_packs_epi32() store of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="values">The values, inside the sixteen-bit range.</param>
        /// <param name="destination">The first value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreShorts(Vector128<int> values, ref short destination)
            => Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref destination), Vector128.Narrow(values, values).AsUInt64().ToScalar());

        /// <summary>
        /// Stores eight saturated intermediate values. Reference: the _mm_packs_epi32() store of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="values">The values, inside the sixteen-bit range.</param>
        /// <param name="destination">The first value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreShorts(Vector256<int> values, ref short destination)
            => Vector128.Narrow(values.GetLower(), values.GetUpper()).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores sixteen saturated intermediate values. Reference: the _mm_packs_epi32() store of av1_convolve_2d_sr_avx2().
        /// </summary>
        /// <param name="values">The values, inside the sixteen-bit range.</param>
        /// <param name="destination">The first value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreShorts(Vector512<int> values, ref short destination)
            => Vector256.Narrow(values.GetLower(), values.GetUpper()).StoreUnsafe(ref destination);
    }
}
