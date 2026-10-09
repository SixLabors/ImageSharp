// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the temporal filter kernels against scalar transcriptions of the libaom definitions, across sample
/// precision, chroma subsampling, weight calculation level and intrinsic tiers.
/// </summary>
[Trait("Format", "Avif")]
public class Av1TemporalFilterTests
{
    /// <summary>
    /// The hardware configurations that run the 512-bit, 256-bit and 128-bit overloads and the scalar overloads.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The chroma layouts of the filter.
    /// </summary>
    private static readonly Av1ColorFormat[] Formats = [Av1ColorFormat.Yuv420, Av1ColorFormat.Yuv422, Av1ColorFormat.Yuv444, Av1ColorFormat.Yuv400];

    /// <summary>
    /// Verifies the weights and accumulators of one filter block against av1_apply_temporal_filter_c(), with the
    /// factor order of the x64 kernels except for 4:2:2 high bit depth, which libaom sends to the C function.
    /// </summary>
    [Fact]
    public void ApplyFilterMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateApplyFilter, Configurations);

    /// <summary>
    /// Verifies the full-weight accumulation against tf_apply_temporal_filter_self() and the normalization against
    /// tf_normalize_filtered_frame().
    /// </summary>
    [Fact]
    public void AccumulateAndNormalizeMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateAccumulateAndNormalize, Configurations);

    /// <summary>
    /// Verifies the noise estimate against av1_estimate_noise_from_single_plane_c() and
    /// av1_highbd_estimate_noise_from_single_plane_c().
    /// </summary>
    [Fact]
    public void NoiseEstimateMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateNoiseEstimate, Configurations);

    /// <summary>
    /// Verifies the twelve-tap MULTITAP_SHARP2 prediction against init_subpel_params() and the single-reference C
    /// convolutions, with the saturated high-bit-depth intermediate of the x64 build.
    /// </summary>
    [Fact]
    public void PredictionMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePrediction, Configurations);

    private static void ValidateApplyFilter()
    {
        Random random = new(0x7F01);
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            foreach (Av1ColorFormat format in Formats)
            {
                foreach (int level in new[] { 0, 1 })
                {
                    for (int trial = 0; trial < 2; trial++)
                    {
                        if (bitDepth == 8)
                        {
                            RunApplyFilter<byte, Av1TemporalFilter.ByteOperator>(random, bitDepth, format, level, trial == 1);
                        }
                        else
                        {
                            RunApplyFilter<ushort, Av1TemporalFilter.UInt16Operator>(random, bitDepth, format, level, trial == 1);
                        }
                    }
                }
            }
        }
    }

    private static void RunApplyFilter<TSample, TOperator>(Random random, int bitDepth, Av1ColorFormat format, int level, bool extreme)
        where TSample : unmanaged
        where TOperator : struct, Av1TemporalFilter.ITemporalFilterOperator<TSample>
    {
        int maximum = (1 << bitDepth) - 1;
        using Av1EncoderFrameBuffer<TSample> buffer = new(Configuration.Default, 64, 64, bitDepth, format, 0, 0, 64);
        using Av1TemporalFilterWorkspace<TSample> workspace = new(Configuration.Default);
        Av1EncoderFrame<TSample> frame = buffer.Frame;
        int planeCount = frame.IsMonochrome ? 1 : 3;
        int subsamplingX = frame.ChromaSubsamplingX;
        int subsamplingY = frame.ChromaSubsamplingY;
        int[][] planes = new int[planeCount][];
        int pixels = 0;
        for (int plane = 0; plane < planeCount; plane++)
        {
            int width = 64 >> (plane == 0 ? 0 : subsamplingX);
            int height = 64 >> (plane == 0 ? 0 : subsamplingY);
            planes[plane] = new int[width * height];
            Av1PlaneRegion<TSample> region = frame.CodedView.GetPlane((Av1Plane)plane);
            for (int y = 0; y < height; y++)
            {
                Span<TSample> row = region.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    // The extreme trial alternates the full range, which gives the largest squared errors.
                    int value = extreme ? ((x + y) & 1) * maximum : random.Next(0, maximum + 1);
                    planes[plane][(y * width) + x] = value;
                    row[x] = FromInt<TSample>(value);
                }
            }

            pixels += width * height;
        }

        int[] prediction = new int[pixels];
        uint[] expectedAccumulator = new uint[pixels];
        ushort[] expectedCount = new ushort[pixels];
        Span<TSample> workspacePrediction = workspace.Prediction;
        Span<uint> accumulator = workspace.Accumulator;
        Span<ushort> count = workspace.Count;
        int offset = 0;
        for (int plane = 0; plane < planeCount; plane++)
        {
            for (int i = 0; i < planes[plane].Length; i++)
            {
                // Predictions close to the source give the full range of weights.
                int spread = extreme ? maximum : maximum >> 4;
                int value = Math.Clamp(planes[plane][i] + random.Next(-spread, spread + 1), 0, maximum);
                prediction[offset + i] = value;
                workspacePrediction[offset + i] = FromInt<TSample>(value);
                expectedAccumulator[offset + i] = (uint)random.Next(0, 1 << 20);
                expectedCount[offset + i] = (ushort)random.Next(0, 4000);
                accumulator[offset + i] = expectedAccumulator[offset + i];
                count[offset + i] = expectedCount[offset + i];
            }

            offset += planes[plane].Length;
        }

        double[] noise = [(random.NextDouble() * 4) - 1, random.NextDouble() * 4, random.NextDouble() * 4];
        Av1MotionVector[] vectors = new Av1MotionVector[16];
        int[] errors = new int[16];
        for (int i = 0; i < 16; i++)
        {
            vectors[i] = new Av1MotionVector(random.Next(-400, 401), random.Next(-400, 401));
            errors[i] = random.Next(0, extreme ? 70000 : 600);
        }

        int qFactor = random.Next(1, 256);
        int strength = random.Next(0, 7);
        bool separateFactors = bitDepth > 8 && format == Av1ColorFormat.Yuv422;
        ReferenceApplyFilter(
            planes, prediction, subsamplingX, subsamplingY, bitDepth, noise, vectors, errors, qFactor, strength, level, separateFactors, 64, expectedAccumulator, expectedCount);

        Av1TemporalFilter.ApplyFilter<TSample, TOperator>(
            workspace,
            frame,
            frame.CodedView.GetPlane(Av1Plane.Y).Samples,
            frame.CodedView.GetPlane(Av1Plane.U).Samples,
            frame.CodedView.GetPlane(Av1Plane.V).Samples,
            0,
            0,
            noise,
            vectors,
            errors,
            qFactor,
            strength,
            level);

        Assert.Equal(expectedAccumulator, accumulator[..pixels].ToArray());
        Assert.Equal(expectedCount, count[..pixels].ToArray());
    }

    private static void ValidateAccumulateAndNormalize()
    {
        Random random = new(0x7F02);
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            foreach (int width in new[] { 16, 32, 64 })
            {
                int maximum = (1 << bitDepth) - 1;
                int stride = width + 11;
                int height = 64;
                ushort[] samples = new ushort[stride * height];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = (ushort)random.Next(0, maximum + 1);
                }

                uint[] accumulator = new uint[width * height];
                ushort[] count = new ushort[width * height];
                uint[] expectedAccumulator = new uint[width * height];
                ushort[] expectedCount = new ushort[width * height];
                for (int i = 0; i < accumulator.Length; i++)
                {
                    // Up to twelve further frames at full weight.
                    count[i] = (ushort)random.Next(0, 12001);
                    accumulator[i] = (uint)(count[i] * random.Next(0, maximum + 1));
                    int y = i / width;
                    int x = i % width;
                    expectedAccumulator[i] = accumulator[i] + (uint)(1000 * samples[(y * stride) + x]);
                    expectedCount[i] = (ushort)(count[i] + 1000);
                }

                if (bitDepth == 8)
                {
                    Av1TemporalFilter.AccumulateSelf<byte, Av1TemporalFilter.ByteOperator>(ToBytes(samples), stride, accumulator, count, width, height);
                }
                else
                {
                    Av1TemporalFilter.AccumulateSelf<ushort, Av1TemporalFilter.UInt16Operator>(samples, stride, accumulator, count, width, height);
                }

                Assert.Equal(expectedAccumulator, accumulator);
                Assert.Equal(expectedCount, count);

                // OD_DIVU() is exact division; the rounding adds half the count.
                ushort[] expected = new ushort[stride * height];
                for (int i = 0; i < accumulator.Length; i++)
                {
                    int y = i / width;
                    int x = i % width;
                    expected[(y * stride) + x] = (ushort)((accumulator[i] + (uint)(count[i] >> 1)) / count[i]);
                }

                if (bitDepth == 8)
                {
                    byte[] actual = new byte[stride * height];
                    Av1TemporalFilter.Normalize<byte, Av1TemporalFilter.ByteOperator>(accumulator, count, width, height, actual, stride);
                    Assert.Equal(ToBytes(expected), actual);
                }
                else
                {
                    ushort[] actual = new ushort[stride * height];
                    Av1TemporalFilter.Normalize<ushort, Av1TemporalFilter.UInt16Operator>(accumulator, count, width, height, actual, stride);
                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    private static void ValidateNoiseEstimate()
    {
        Random random = new(0x7F03);
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            foreach ((int width, int height) in new[] { (64, 64), (67, 45), (130, 40), (31, 29), (200, 3), (640, 24), (1003, 9) })
            {
                foreach (int amplitude in new[] { 2, 40, 1 << bitDepth })
                {
                    int stride = width + 5;
                    int maximum = (1 << bitDepth) - 1;
                    int center = 1 << (bitDepth - 1);
                    ushort[] samples = new ushort[stride * height];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        // A flat plane with small noise has many smooth samples; the full amplitude has few.
                        int value = center + random.Next(-amplitude, amplitude + 1);
                        samples[i] = (ushort)Math.Clamp(value, 0, maximum);
                    }

                    double expected = ReferenceNoise(samples, stride, width, height, bitDepth, Av1TemporalFilter.NoiseEdgeThreshold);
                    double actual = bitDepth == 8
                        ? Av1TemporalFilter.EstimateNoise<byte, Av1TemporalFilter.ByteOperator>(ToBytes(samples), stride, width, height, bitDepth, Av1TemporalFilter.NoiseEdgeThreshold)
                        : Av1TemporalFilter.EstimateNoise<ushort, Av1TemporalFilter.UInt16Operator>(samples, stride, width, height, bitDepth, Av1TemporalFilter.NoiseEdgeThreshold);

                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    private static void ValidatePrediction()
    {
        const int border = 300;
        const int planeSize = 64;
        Random random = new(0x7F04);
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int maximum = (1 << bitDepth) - 1;
            int stride = planeSize + (2 * border);
            ushort[] samples = new ushort[stride * stride];
            for (int i = 0; i < samples.Length; i++)
            {
                // Alternating extremes at the central phases drive the high-bit-depth intermediate into saturation.
                samples[i] = (ushort)(random.Next(4) == 0 ? random.Next(0, maximum + 1) : (i & 1) * maximum);
            }

            byte[] bytes = ToBytes(samples);
            int origin = (border * stride) + border;
            short[] intermediate = new short[Av1TemporalFilter.PredictionIntermediateLength];
            for (int trial = 0; trial < 300; trial++)
            {
                int subsamplingX = random.Next(2);
                int subsamplingY = subsamplingX == 0 ? 0 : random.Next(2);
                int width = 16 >> subsamplingX;
                int height = 16 >> subsamplingY;
                int y = random.Next(0, 4) * height;
                int x = random.Next(0, 4) * width;

                // Large vectors reach the clamp of init_subpel_params() in both directions.
                int range = trial % 5 == 0 ? 3000 : 400;
                Av1MotionVector vector = new(random.Next(-range, range + 1), random.Next(-range, range + 1));
                if (trial % 7 == 0)
                {
                    vector = new Av1MotionVector(vector.Row & ~15, vector.Column);
                }

                if (trial % 11 == 0)
                {
                    vector = new Av1MotionVector(vector.Row, vector.Column & ~15);
                }

                int planeWidth = planeSize >> subsamplingX;
                int planeHeight = planeSize >> subsamplingY;
                int[] expected = ReferencePrediction(samples, stride, origin, planeWidth, planeHeight, y, x, subsamplingX, subsamplingY, vector, width, height, bitDepth);
                Av1TemporalFilter.ConvolveTerms terms = new(bitDepth);
                if (bitDepth == 8)
                {
                    byte[] actual = new byte[width * height];
                    Av1TemporalFilter.PredictSubblock<byte, Av1TemporalFilter.ByteOperator>(
                        bytes, stride, origin, planeWidth, planeHeight, y, x, subsamplingX, subsamplingY, vector, width, height, in terms, intermediate, actual, width);

                    Assert.Equal(expected, Array.ConvertAll(actual, value => (int)value));
                }
                else
                {
                    ushort[] actual = new ushort[width * height];
                    Av1TemporalFilter.PredictSubblock<ushort, Av1TemporalFilter.UInt16Operator>(
                        samples, stride, origin, planeWidth, planeHeight, y, x, subsamplingX, subsamplingY, vector, width, height, in terms, intermediate, actual, width);

                    Assert.Equal(expected, Array.ConvertAll(actual, value => (int)value));
                }
            }
        }
    }

    /// <summary>
    /// The scalar definition of av1_apply_temporal_filter_c() for one 64x64 block. The x64 kernels multiply the
    /// combined error by the product of the distance and decay factors; the C function multiplies by each in turn.
    /// </summary>
    private static void ReferenceApplyFilter(
        int[][] planes,
        int[] prediction,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        double[] noise,
        Av1MotionVector[] vectors,
        int[] errors,
        int qFactor,
        int strength,
        int level,
        bool separateFactors,
        int minimumFrameSize,
        uint[] accumulator,
        ushort[] count)
    {
        double inverseFactor = 1.0 / ((5 + 1) * 20);
        double weightFactor = 5.0 * inverseFactor;
        double qDecay = Math.Pow((double)qFactor / 20, 2);
        qDecay = qDecay < 1e-5 ? 1e-5 : qDecay > 1 ? 1 : qDecay;
        if (qFactor >= 128)
        {
            qDecay = 0.5 * Math.Pow((double)qFactor / 64, 2);
        }

        double strengthDecay = Math.Pow((double)strength / 4, 2);
        strengthDecay = strengthDecay < 1e-5 ? 1e-5 : strengthDecay > 1 ? 1 : strengthDecay;
        double[] distanceFactors = new double[16];
        for (int i = 0; i < 16; i++)
        {
            double distance = Math.Sqrt(Math.Pow(vectors[i].Row, 2) + Math.Pow(vectors[i].Column, 2));
            double threshold = Math.Max(minimumFrameSize * 0.1, 1);
            distanceFactors[i] = Math.Max(distance / threshold, 1);
        }

        uint[] squares = new uint[64 * 64];
        uint[] lumaSums = new uint[64 * 64];
        int planeOffset = 0;
        for (int plane = 0; plane < planes.Length; plane++)
        {
            int shiftX = plane == 0 ? 0 : subsamplingX;
            int shiftY = plane == 0 ? 0 : subsamplingY;
            int height = 64 >> shiftY;
            int width = 64 >> shiftX;
            double inverseReferenceCount = 1.0 / (25 + (plane == 0 ? 0 : 1 << (shiftX + shiftY)));
            double decay = 1 / ((0.5 + Math.Log((2 * noise[plane]) + 5.0)) * qDecay * strengthDecay);
            if (plane == 1)
            {
                // compute_luma_sq_error_sum()
                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        for (int ii = 0; ii < (1 << shiftY); ii++)
                        {
                            for (int jj = 0; jj < (1 << shiftX); jj++)
                            {
                                lumaSums[(i * width) + j] += squares[(((i << shiftY) + ii) * (width << shiftX)) + (j << shiftX) + jj];
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < width * height; i++)
            {
                int difference = planes[plane][i] - prediction[planeOffset + i];
                squares[i] = (uint)(difference * difference);
            }

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    ulong sum = 0;
                    for (int wi = -2; wi <= 2; wi++)
                    {
                        for (int wj = -2; wj <= 2; wj++)
                        {
                            sum += squares[(Math.Clamp(i + wi, 0, height - 1) * width) + Math.Clamp(j + wj, 0, width - 1)];
                        }
                    }

                    sum += lumaSums[(i * width) + j];
                    if (bitDepth > 8)
                    {
                        sum >>= (bitDepth - 8) * 2;
                    }

                    double windowError = sum * inverseReferenceCount;
                    int y32 = i / (height / 2);
                    int x32 = j / (width / 2);
                    int y16 = (i % (height / 2)) / (height / 4);
                    int x16 = (j % (width / 2)) / (width / 4);
                    int index = (((y32 * 2) + x32) * 4) + (y16 * 2) + x16;
                    double combined = (weightFactor * windowError) + (errors[index] * inverseFactor);
                    double scaled = separateFactors
                        ? combined * distanceFactors[index] * decay
                        : combined * (distanceFactors[index] * decay);

                    scaled = Math.Min(scaled, 7);
                    int weight;
                    if (level == 0)
                    {
                        weight = (int)(Math.Exp(-scaled) * 1000);
                    }
                    else
                    {
                        float y = (float)-scaled;
                        float approximation = BitConverter.Int32BitsToSingle((int)(y * ((1 << 23) / 0.69314718056f)) + ((127 << 23) - 60801));
                        weight = (int)((approximation * 1000) + 0.5f);
                    }

                    int position = planeOffset + (i * width) + j;
                    accumulator[position] += (uint)(weight * prediction[position]);
                    count[position] = (ushort)(count[position] + weight);
                }
            }

            planeOffset += width * height;
        }
    }

    /// <summary>
    /// The scalar definition of av1_estimate_noise_from_single_plane_c() and
    /// av1_highbd_estimate_noise_from_single_plane_c().
    /// </summary>
    private static double ReferenceNoise(ushort[] samples, int stride, int width, int height, int bitDepth, int edgeThreshold)
    {
        long accumulated = 0;
        int count = 0;
        int shift = bitDepth - 8;
        int bias = (1 << shift) >> 1;
        for (int i = 1; i < height - 1; i++)
        {
            for (int j = 1; j < width - 1; j++)
            {
                int k = (i * stride) + j;
                int a = samples[k - stride - 1];
                int b = samples[k - stride];
                int c = samples[k - stride + 1];
                int d = samples[k - 1];
                int e = samples[k];
                int f = samples[k + 1];
                int g = samples[k + stride - 1];
                int h = samples[k + stride];
                int l = samples[k + stride + 1];
                int gx = (a - c) + (g - l) + (2 * (d - f));
                int gy = (a - g) + (c - l) + (2 * (b - h));
                int gradient = (Math.Abs(gx) + Math.Abs(gy) + bias) >> shift;
                if (gradient < edgeThreshold)
                {
                    int v = (4 * e) - (2 * (b + h + d + f)) + (a + c + g + l);
                    accumulated += (Math.Abs(v) + bias) >> shift;
                    count++;
                }
            }
        }

        return count < 16 ? -1.0 : (double)accumulated / (6 * count) * 1.25331413732;
    }

    /// <summary>
    /// The scalar definition of av1_enc_build_one_inter_predictor() with MULTITAP_SHARP2: init_subpel_params(),
    /// then aom_convolve_copy(), av1_convolve_x_sr_c(), av1_convolve_y_sr_c() or av1_convolve_2d_sr_c(), with the
    /// two-dimensional intermediate saturated to sixteen bits as the x64 kernels pack it.
    /// </summary>
    private static int[] ReferencePrediction(
        ushort[] samples,
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
        int bitDepth)
    {
        short[] kernels =
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

        int positionY = (((y << 4) + (vector.Row * (1 << (1 - subsamplingY)))) << 6) + 32;
        int positionX = (((x << 4) + (vector.Column * (1 << (1 - subsamplingX)))) << 6) + 32;
        positionY = Math.Clamp(positionY, -(((288 >> subsamplingY) - 4) << 10), (planeHeight + 4) << 10);
        positionX = Math.Clamp(positionX, -(((288 >> subsamplingX) - 4) << 10), (planeWidth + 4) << 10);
        int phaseY = (positionY & 1023) >> 6;
        int phaseX = (positionX & 1023) >> 6;
        int start = origin + ((positionY >> 10) * stride) + (positionX >> 10);
        int maximum = (1 << bitDepth) - 1;
        int round0 = bitDepth == 12 ? 5 : 3;
        int round1 = 14 - round0;
        int[] result = new int[width * height];
        int[] intermediate = new int[(height + 11) * width];
        for (int row = -5; row < height + 6; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int sum = 0;
                for (int k = 0; k < 12; k++)
                {
                    sum += kernels[(phaseX * 12) + k] * samples[start + (row * stride) + column - 5 + k];
                }

                intermediate[((row + 5) * width) + column] = sum;
            }
        }

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int value;
                if (phaseX == 0 && phaseY == 0)
                {
                    value = samples[start + (row * stride) + column];
                }
                else if (phaseY == 0)
                {
                    int first = (intermediate[((row + 5) * width) + column] + (1 << (round0 - 1))) >> round0;
                    value = Math.Clamp((first + (1 << (7 - round0 - 1))) >> (7 - round0), 0, maximum);
                }
                else if (phaseX == 0)
                {
                    int sum = 0;
                    for (int k = 0; k < 12; k++)
                    {
                        sum += kernels[(phaseY * 12) + k] * samples[start + ((row - 5 + k) * stride) + column];
                    }

                    value = Math.Clamp((sum + 64) >> 7, 0, maximum);
                }
                else
                {
                    int sum = 1 << (bitDepth + 14 - round0);
                    for (int k = 0; k < 12; k++)
                    {
                        int horizontal = intermediate[((row + k) * width) + column] + (1 << (bitDepth + 6));
                        int stored = Math.Clamp((horizontal + (1 << (round0 - 1))) >> round0, short.MinValue, short.MaxValue);
                        sum += kernels[(phaseY * 12) + k] * stored;
                    }

                    int rounded = ((sum + (1 << (round1 - 1))) >> round1) - ((1 << bitDepth) + (1 << (bitDepth - 1)));
                    value = Math.Clamp(rounded, 0, maximum);
                }

                result[(row * width) + column] = value;
            }
        }

        return result;
    }

    private static TSample FromInt<TSample>(int value)
        where TSample : unmanaged
        => typeof(TSample) == typeof(byte) ? (TSample)(object)(byte)value : (TSample)(object)(ushort)value;

    private static byte[] ToBytes(ushort[] values) => Array.ConvertAll(values, value => (byte)value);
}
