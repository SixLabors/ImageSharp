// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Combines fixed-point AV1 rate and distortion values for encoder decisions.
/// </summary>
internal static class Av1RateDistortion
{
    /// <summary>
    /// Each fitted curve contains 65 equally spaced samples, including the cubic interpolation endpoints.
    /// </summary>
    private const int ModelCurveLength = 65;

    /// <summary>
    /// Gets normalized Laplacian entropy in Q10 units.
    /// </summary>
    private static ReadOnlySpan<int> LaplacianRates =>
    [
        65536, 6086, 5574, 5275, 5063, 4899, 4764, 4651, 4553, 4389, 4255, 4142,
        4044, 3958, 3881, 3811, 3748, 3635, 3538, 3453, 3376, 3307, 3244, 3186,
        3133, 3037, 2952, 2877, 2809, 2747, 2690, 2638, 2589, 2501, 2423, 2353,
        2290, 2232, 2179, 2130, 2084, 2001, 1928, 1862, 1802, 1748, 1698, 1651,
        1608, 1530, 1460, 1398, 1342, 1290, 1243, 1199, 1159, 1086, 1021, 963,
        911, 864, 821, 781, 745, 680, 623, 574, 530, 490, 455, 424,
        395, 345, 304, 269, 239, 213, 190, 171, 154, 126, 104, 87,
        73, 61, 52, 44, 38, 28, 21, 16, 12, 10, 8, 6,
        5, 3, 2, 1, 1, 1, 0, 0,
    ];

    /// <summary>
    /// Gets normalized Laplacian distortion in Q10 units.
    /// </summary>
    private static ReadOnlySpan<int> LaplacianDistortions =>
    [
        0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 5,
        5, 6, 7, 7, 8, 9, 11, 12, 13, 15, 16, 17,
        18, 21, 24, 26, 29, 31, 34, 36, 39, 44, 49, 54,
        59, 64, 69, 73, 78, 88, 97, 106, 115, 124, 133, 142,
        151, 167, 184, 200, 215, 231, 245, 260, 274, 301, 327, 351,
        375, 397, 418, 439, 458, 495, 528, 559, 587, 613, 637, 659,
        680, 717, 749, 777, 801, 823, 842, 859, 874, 899, 919, 936,
        949, 960, 969, 977, 983, 994, 1001, 1006, 1010, 1013, 1015, 1017,
        1018, 1020, 1022, 1022, 1023, 1023, 1023, 1024,
    ];

    /// <summary>
    /// Gets squared quantizer-to-variance ratios for the logarithmically spaced model samples.
    /// </summary>
    private static ReadOnlySpan<int> LaplacianSamplePoints =>
    [
        0, 4, 8, 12, 16, 20, 24, 28, 32, 40, 48, 56,
        64, 72, 80, 88, 96, 112, 128, 144, 160, 176, 192, 208,
        224, 256, 288, 320, 352, 384, 416, 448, 480, 544, 608, 672,
        736, 800, 864, 928, 992, 1120, 1248, 1376, 1504, 1632, 1760, 1888,
        2016, 2272, 2528, 2784, 3040, 3296, 3552, 3808, 4064, 4576, 5088, 5600,
        6112, 6624, 7136, 7648, 8160, 9184, 10208, 11232, 12256, 13280, 14304, 15328,
        16352, 18400, 20448, 22496, 24544, 26592, 28640, 30688, 32736, 36832, 40928, 45024,
        49120, 53216, 57312, 61408, 65504, 73696, 81888, 90080, 98272, 106464, 114656, 122848,
        131040, 147424, 163808, 180192, 196576, 212960, 229344, 245728,
    ];

    /// <summary>
    /// Gets the rate-curve category for each AV1 block geometry.
    /// </summary>
    private static ReadOnlySpan<byte> ModelRateCategories => [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 1, 1, 2, 2, 3, 3];

    /// <summary>
    /// Gets the four block-size rate curves in fixed-point bit-cost units per sample.
    /// </summary>
    private static ReadOnlySpan<double> ModelRateCurves =>
    [
        0.000000, 0.000000, 0.000000, 0.000000, 0.000000,
        0.000000, 0.000000, 0.000000, 0.000000, 0.000000,
        0.000000, 118.257702, 120.210658, 121.434853, 122.100487,
        122.377758, 122.436865, 72.290102, 96.974289, 101.652727,
        126.830141, 140.417377, 157.644879, 184.315291, 215.823873,
        262.300169, 335.919859, 420.624173, 519.185032, 619.854243,
        726.053595, 827.663369, 933.127475, 1037.988755, 1138.839609,
        1233.342933, 1333.508064, 1428.760126, 1533.396364, 1616.952052,
        1744.539319, 1803.413586, 1951.466618, 1994.227838, 2086.031680,
        2148.635443, 2239.068450, 2222.590637, 2338.859809, 2402.929011,
        2418.727875, 2435.342670, 2471.159469, 2523.187446, 2591.183827,
        2674.905840, 2774.110714, 2888.555675, 3017.997952, 3162.194773,
        3320.903365, 3493.880956, 3680.884773, 3881.672045, 4096.000000,
        0.000000, 0.000000, 0.000000, 0.000000, 0.000000,
        0.000000, 0.000000, 0.000000, 0.000000, 0.000000,
        0.000000, 13.087244, 15.919735, 25.930313, 24.412411,
        28.567417, 29.924194, 30.857010, 32.742979, 36.382570,
        39.210386, 42.265690, 47.378572, 57.014850, 82.740067,
        137.346562, 219.968084, 316.781856, 415.643773, 516.706538,
        614.914364, 714.303763, 815.512135, 911.210485, 1008.501528,
        1109.787854, 1213.772279, 1322.922561, 1414.752579, 1510.505641,
        1615.741888, 1697.989032, 1780.123933, 1847.453790, 1913.742309,
        1960.828122, 2047.500168, 2085.454095, 2129.230668, 2158.171824,
        2182.231724, 2217.684864, 2269.589211, 2337.264824, 2420.618694,
        2519.557814, 2633.989178, 2763.819779, 2908.956609, 3069.306660,
        3244.776927, 3435.274401, 3640.706076, 3860.978945, 4096.000000,
        0.000000, 0.000000, 0.000000, 0.000000, 0.000000,
        0.000000, 0.000000, 0.000000, 0.000000, 0.000000,
        0.000000, 4.656893, 5.123633, 5.594132, 6.162376,
        6.918433, 7.768444, 8.739415, 10.105862, 11.477328,
        13.236604, 15.421030, 19.093623, 25.801871, 46.724612,
        98.841054, 181.113466, 272.586364, 359.499769, 445.546343,
        525.944439, 605.188743, 681.793483, 756.668359, 838.486885,
        926.950356, 1015.482542, 1113.353926, 1204.897193, 1288.871992,
        1373.464145, 1455.746628, 1527.796460, 1588.475066, 1658.144771,
        1710.302500, 1807.563351, 1863.197608, 1927.281616, 1964.450872,
        2022.719898, 2100.041145, 2185.205712, 2280.993936, 2387.616216,
        2505.282950, 2634.204540, 2774.591385, 2926.653884, 3090.602436,
        3266.647443, 3454.999303, 3655.868416, 3869.465182, 4096.000000,
        0.000000, 0.000000, 0.000000, 0.000000, 0.000000,
        0.000000, 0.000000, 0.000000, 0.000000, 0.000000,
        0.000000, 0.337370, 0.391916, 0.468839, 0.566334,
        0.762564, 1.069225, 1.384361, 1.787581, 2.293948,
        3.251909, 4.412991, 8.050068, 11.606073, 27.668092,
        65.227758, 128.463938, 202.097653, 262.715851, 312.464873,
        355.601398, 400.609054, 447.201352, 495.761568, 552.871938,
        619.067625, 691.984883, 773.753288, 860.628503, 946.262808,
        1019.805896, 1106.061360, 1178.422145, 1244.852258, 1302.173987,
        1399.650266, 1548.092912, 1545.928652, 1670.817500, 1694.523823,
        1779.195362, 1882.155494, 1990.662097, 2108.325181, 2235.456119,
        2372.366287, 2519.367059, 2676.769812, 2844.885918, 3024.026754,
        3214.503695, 3416.628115, 3630.711389, 3857.064892, 4096.000000,
    ];

    /// <summary>
    /// Gets the low- and high-error distortion curves in sixteenth-sample-error units.
    /// </summary>
    private static ReadOnlySpan<double> ModelDistortionCurves =>
    [
        16.000000, 15.962891, 15.925174, 15.886888, 15.848074,
        15.808770, 15.769015, 15.728850, 15.688313, 15.647445,
        15.606284, 15.564870, 15.525918, 15.483820, 15.373330,
        15.126844, 14.637442, 14.184387, 13.560070, 12.880717,
        12.165995, 11.378144, 10.438769, 9.130790, 7.487633,
        5.688649, 4.267515, 3.196300, 2.434201, 1.834064,
        1.369920, 1.035921, 0.775279, 0.574895, 0.427232,
        0.314123, 0.233236, 0.171440, 0.128188, 0.092762,
        0.067569, 0.049324, 0.036330, 0.027008, 0.019853,
        0.015539, 0.011093, 0.008733, 0.007624, 0.008105,
        0.005427, 0.004065, 0.003427, 0.002848, 0.002328,
        0.001865, 0.001457, 0.001103, 0.000801, 0.000550,
        0.000348, 0.000193, 0.000085, 0.000021, 0.000000,
        16.000000, 15.996116, 15.984769, 15.966413, 15.941505,
        15.910501, 15.873856, 15.832026, 15.785466, 15.734633,
        15.679981, 15.621967, 15.560961, 15.460157, 15.288367,
        15.052462, 14.466922, 13.921212, 13.073692, 12.222005,
        11.237799, 9.985848, 8.898823, 7.423519, 5.995325,
        4.773152, 3.744032, 2.938217, 2.294526, 1.762412,
        1.327145, 1.020728, 0.765535, 0.570548, 0.425833,
        0.313825, 0.232959, 0.171324, 0.128174, 0.092750,
        0.067558, 0.049319, 0.036330, 0.027008, 0.019853,
        0.015539, 0.011093, 0.008733, 0.007624, 0.008105,
        0.005427, 0.004065, 0.003427, 0.002848, 0.002328,
        0.001865, 0.001457, 0.001103, 0.000801, 0.000550,
        0.000348, 0.000193, 0.000085, 0.000021, -0.000000,
    ];

    /// <summary>
    /// Estimates quantized residual rate and distortion with a Laplacian source model.
    /// </summary>
    /// <param name="variance">The summed residual energy in the normalized sample domain.</param>
    /// <param name="sampleCountLog2">The base-two logarithm of the sample count.</param>
    /// <param name="quantizerStep">The quantizer step after removing transform scaling.</param>
    /// <param name="rate">The modeled rate in 1/512-bit units.</param>
    /// <param name="distortion">The modeled sample-domain distortion.</param>
    public static void EstimateLaplacian(long variance, int sampleCountLog2, int quantizerStep, out int rate, out long distortion)
    {
        if (variance == 0)
        {
            rate = 0;
            distortion = 0;
            return;
        }

        // Normalize the squared quantizer step by mean residual energy. Saturating at the last
        // interpolation interval represents complete coefficient suppression without indexing past it.
        ulong squaredRatio = ((((ulong)quantizerStep * (uint)quantizerStep) << (sampleCountLog2 + 10)) + (ulong)(variance >> 1)) /
            (ulong)variance;

        int ratio = (int)Math.Min(squaredRatio, 245727UL);
        int position = (ratio >> 2) + 8;
        int exponent = System.Numerics.BitOperations.Log2((uint)position) - 3;
        int index = (exponent << 3) + ((position >> exponent) & 7);

        // Each octave has eight linear intervals. Both weights are Q10, so interpolation retains
        // ten fractional bits before rate scaling and the final rounded energy multiplication.
        int upperWeight = ((ratio - LaplacianSamplePoints[index]) << 10) >> (2 + exponent);
        int lowerWeight = 1024 - upperWeight;
        int normalizedRate = ((LaplacianRates[index] * lowerWeight) + (LaplacianRates[index + 1] * upperWeight)) >> 10;
        int normalizedDistortion = ((LaplacianDistortions[index] * lowerWeight) +
            (LaplacianDistortions[index + 1] * upperWeight)) >> 10;

        int rateShift = 10 - Av1ProbabilityCost.CostShift;
        rate = ((normalizedRate << sampleCountLog2) + (1 << (rateShift - 1))) >> rateShift;
        distortion = ((variance * normalizedDistortion) + 512) >> 10;
    }

    /// <summary>
    /// Gets the rate multiplier for a quantizer, sample precision, and frame update role.
    /// </summary>
    /// <param name="qIndex">The segment quantizer index including its luma DC delta.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="updateType">The frame's role in the reference update schedule.</param>
    /// <param name="tuning">The tune metric.</param>
    /// <param name="realtime">Whether the encoder runs in real-time usage.</param>
    /// <returns>The rate multiplier.</returns>
    /// <remarks>Reference: av1_compute_rd_mult_based_on_qindex().</remarks>
    public static int GetRateMultiplier(
        int qIndex,
        Av1BitDepth bitDepth,
        Av1FrameUpdateType updateType,
        Av1Tuning tuning = Av1Tuning.Psnr,
        bool realtime = false)
    {
        int quantizer = Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth);
        double baseWeight = updateType switch
        {
            Av1FrameUpdateType.Key => 3.3,
            Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate => 3.25,
            _ => 3.2
        };

        // The squared DC step sets the distortion scale. Reference-producing golden/alternate pictures
        // use the intermediate weight; overlay and intermediate-alternate roles retain the ordinary weight.
        // Truncate the weighted product before rounding high-bit-depth distortion into the eight-bit domain.
        long multiplier = (long)((quantizer * (long)quantizer) * (baseWeight + (0.0015 * quantizer)));

        // The image tune scales the multiplier by up to 200/128, falling to unity at the highest quantizers, which
        // favors larger transforms. Real-time usage uses a quarter instead.
        if (tuning == Av1Tuning.Iq)
        {
            int weight = realtime ? 32 : Math.Clamp(((255 - qIndex) * 3) / 4, 0, 72) + 128;
            multiplier = (long)(multiplier * (double)weight / 128.0);
        }

        int shift = (bitDepth.GetBitCount() - 8) * 2;
        if (shift > 0)
        {
            multiplier = (multiplier + (1L << (shift - 1))) >> shift;
        }

        return (int)Math.Clamp(multiplier, 1, int.MaxValue);
    }

    /// <summary>
    /// Gets a rate-distortion cost using the encoder probability-cost precision.
    /// </summary>
    /// <param name="rateMultiplier">The rate weight selected by the encoder quality model.</param>
    /// <param name="rate">The syntax rate in 1/512-bit units.</param>
    /// <param name="distortion">The sample-domain distortion.</param>
    /// <returns>The rounded weighted rate plus distortion.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetCost(long rateMultiplier, int rate, long distortion)
    {
        long weightedRate = (long)rate * rateMultiplier;
        long roundedRate = (weightedRate + (1 << (Av1ProbabilityCost.CostShift - 1))) >> Av1ProbabilityCost.CostShift;
        return roundedRate + (distortion << 7);
    }

    /// <summary>
    /// Gets the variance-domain cost of a full-pixel motion candidate.
    /// </summary>
    /// <param name="rateMultiplier">The rate weight selected by the encoder quality model.</param>
    /// <param name="motionVectorRate">The motion-vector syntax rate in 1/512-bit units.</param>
    /// <param name="variance">The normalized sample variance.</param>
    /// <returns>The variance plus the motion-vector error cost.</returns>
    public static int GetMotionSearchCost(int rateMultiplier, int motionVectorRate, int variance)
    {
        const int RateMultiplierShift = 6;
        const int MotionErrorShift = 14;
        int errorPerBit = Math.Max(rateMultiplier >> RateMultiplierShift, 1);

        // Motion search compares pixel variance directly, so the syntax term is reduced to the same
        // error domain instead of using the final mode-decision distortion scale.
        long weightedRate = (long)motionVectorRate * errorPerBit;
        int motionError = (int)((weightedRate + (1 << (MotionErrorShift - 1))) >> MotionErrorShift);
        return variance + motionError;
    }

    /// <summary>
    /// Gets the sum-of-absolute-differences rate scale for a frame quantizer.
    /// </summary>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The multiplier that converts motion-vector rate into the absolute-difference domain.</returns>
    public static int GetMotionSearchSadPerBit(int qIndex, Av1BitDepth bitDepth)
    {
        int quantizerDivisor = 1 << (bitDepth.GetBitCount() - 6);
        double quantizer = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth) / (double)quantizerDivisor;
        return (int)((0.0418 * quantizer) + 2.4107);
    }

    /// <summary>
    /// Gets the sum-of-absolute-differences cost of a full-pixel motion candidate.
    /// </summary>
    /// <param name="sadPerBit">The quantizer-derived motion-rate scale.</param>
    /// <param name="motionVectorRate">The motion-vector syntax rate in 1/512-bit units.</param>
    /// <param name="sumOfAbsoluteDifferences">The unnormalized sample-domain absolute difference.</param>
    /// <returns>The absolute difference plus the motion-vector search cost.</returns>
    public static int GetMotionSearchSadCost(int sadPerBit, int motionVectorRate, int sumOfAbsoluteDifferences)
    {
        const int MotionRateShift = 9;

        // Full-pixel traversal uses absolute differences, so its quantizer-derived rate scale is deliberately
        // distinct from the variance-domain error-per-bit scale used to compare the resulting search paths.
        long weightedRate = (long)motionVectorRate * sadPerBit;
        int motionError = (int)((weightedRate + (1 << (MotionRateShift - 1))) >> MotionRateShift);
        return sumOfAbsoluteDifferences + motionError;
    }

    /// <summary>
    /// Estimates residual rate and distortion from prediction error without running transforms or quantization.
    /// </summary>
    /// <param name="blockSize">The plane block geometry selecting the fitted rate curve.</param>
    /// <param name="squaredError">The visible prediction error normalized to eight-bit precision.</param>
    /// <param name="sampleCount">The number of visible samples contributing to the error.</param>
    /// <param name="acQuantizer">The plane AC dequantization step at native sample precision.</param>
    /// <param name="bitDepth">The native sample precision.</param>
    /// <param name="rateMultiplier">The block's rate-distortion multiplier.</param>
    /// <param name="rate">The estimated residual rate in 1/512-bit units.</param>
    /// <param name="distortion">The estimated residual distortion in sixteenth-sample-error units.</param>
    public static void ModelPredictionError(
        Av1BlockSize blockSize,
        long squaredError,
        int sampleCount,
        int acQuantizer,
        Av1BitDepth bitDepth,
        int rateMultiplier,
        out int rate,
        out long distortion)
    {
        if (squaredError == 0)
        {
            rate = 0;
            distortion = 0;
            return;
        }

        const double CurveStart = -15.5;
        const double CurveStep = 0.5;
        const double EndpointMargin = 1E-6;
        const double HighErrorThreshold = 16;
        const int DistortionScaleShift = 4;

        // Transform dequantizers are scaled by eight. Normalize both their precision and the prediction error
        // before taking the logarithmic feature, so the same fitted curves serve eight-, ten-, and twelve-bit input.
        int quantizerStep = Math.Max(acQuantizer >> (bitDepth.GetBitCount() - 5), 1);
        double normalizedError = (double)squaredError / sampleCount;
        double feature = Math.Log2(normalizedError / ((double)quantizerStep * quantizerStep));
        double lastCurvePosition = CurveStart + ((ModelCurveLength - 1) * CurveStep);
        feature = Math.Clamp(feature, CurveStart + CurveStep + EndpointMargin, lastCurvePosition - CurveStep - EndpointMargin);
        double position = (feature - CurveStart) / CurveStep;
        int index = (int)position;
        double fraction = position - index;
        int rateCategory = ModelRateCategories[(int)blockSize];
        int distortionCategory = normalizedError > HighErrorThreshold ? 1 : 0;
        ReadOnlySpan<double> ratePoints = ModelRateCurves.Slice((rateCategory * ModelCurveLength) + index - 1, 4);
        ReadOnlySpan<double> distortionPoints = ModelDistortionCurves.Slice((distortionCategory * ModelCurveLength) + index - 1, 4);
        double rateEstimate;
        double distortionEstimate;
        if (Vector128.IsHardwareAccelerated)
        {
            // The two lanes evaluate rate and distortion together. Keep the cubic polynomial's operation order,
            // including its separate multiplies and adds, so vector and scalar rounding agree at decision boundaries.
            Vector128<double> p0 = Vector128.Create(ratePoints[0], distortionPoints[0]);
            Vector128<double> p1 = Vector128.Create(ratePoints[1], distortionPoints[1]);
            Vector128<double> p2 = Vector128.Create(ratePoints[2], distortionPoints[2]);
            Vector128<double> p3 = Vector128.Create(ratePoints[3], distortionPoints[3]);
            Vector128<double> x = Vector128.Create(fraction);
            Vector128<double> cubic = (Vector128.Create(3.0) * (p1 - p2)) + p3 - p0;
            Vector128<double> quadratic = (Vector128.Create(2.0) * p0) - (Vector128.Create(5.0) * p1) + (Vector128.Create(4.0) * p2) - p3;
            Vector128<double> result = p1 + (Vector128.Create(0.5) * x * (p2 - p0 + (x * (quadratic + (x * cubic)))));

            rateEstimate = result.GetElement(0);
            distortionEstimate = result.GetElement(1);
        }
        else
        {
            rateEstimate = InterpolateModelCurve(ratePoints, fraction);
            distortionEstimate = InterpolateModelCurve(distortionPoints, fraction);
        }

        rate = (int)(Math.Max(0, rateEstimate * sampleCount) + 0.5);
        distortion = (long)(Math.Max(0, (distortionEstimate * normalizedError) * sampleCount) + 0.5);
        long skipDistortion = squaredError << DistortionScaleShift;

        // A modeled coded residual is useful only if it beats leaving the prediction unchanged. Preserve the
        // reference model's zero-rate rule instead of returning an artificially low distortion for a skipped block.
        if (rate == 0 || GetCost(rateMultiplier, rate, distortion) >= GetCost(rateMultiplier, 0, skipDistortion))
        {
            rate = 0;
            distortion = skipDistortion;
        }
    }

    /// <summary>
    /// Evaluates one fitted curve's cubic segment without fusing arithmetic operations.
    /// </summary>
    private static double InterpolateModelCurve(ReadOnlySpan<double> points, double fraction)
    {
        double cubic = (3.0 * (points[1] - points[2])) + points[3] - points[0];
        double quadratic = (2.0 * points[0]) - (5.0 * points[1]) + (4.0 * points[2]) - points[3];
        return points[1] + (0.5 * fraction * (points[2] - points[0] + (fraction * (quadratic + (fraction * cubic)))));
    }
}
