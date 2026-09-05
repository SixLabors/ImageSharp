// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
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
    /// Gets the key-frame rate multiplier for an AV1 quantizer and sample precision.
    /// </summary>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The rate multiplier.</returns>
    public static int GetKeyFrameRateMultiplier(int qIndex, Av1BitDepth bitDepth)
    {
        int quantizer = Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth);

        // Key frames use a quantizer-dependent weight over the squared DC step. High-bit-depth
        // distortion is normalized back to the eight-bit domain, so its rate multiplier follows it.
        long multiplier = (long)((quantizer * (long)quantizer) * (3.3 + (0.0015 * quantizer)));
        int shift = (bitDepth.GetBitCount() - 8) * 2;
        if (shift > 0)
        {
            multiplier = (multiplier + (1L << (shift - 1))) >> shift;
        }

        return (int)Math.Max(multiplier, 1);
    }

    /// <summary>
    /// Gets the inter-frame rate multiplier for an AV1 quantizer and sample precision.
    /// </summary>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The rate multiplier.</returns>
    public static int GetInterFrameRateMultiplier(int qIndex, Av1BitDepth bitDepth)
    {
        int quantizer = Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth);

        // Ordinary inter frames use a slightly lower rate weight than key frames, preserving more residual detail.
        // Distortion remains normalized to the eight-bit domain before it is combined with this value.
        long multiplier = (long)((quantizer * (long)quantizer) * (3.2 + (0.0015 * quantizer)));
        int shift = (bitDepth.GetBitCount() - 8) * 2;
        if (shift > 0)
        {
            multiplier = (multiplier + (1L << (shift - 1))) >> shift;
        }

        return (int)Math.Max(multiplier, 1);
    }

    /// <summary>
    /// Gets a rate-distortion cost using the encoder probability-cost precision.
    /// </summary>
    /// <param name="rateMultiplier">The rate weight selected by the encoder quality model.</param>
    /// <param name="rate">The syntax rate in 1/512-bit units.</param>
    /// <param name="distortion">The sample-domain distortion.</param>
    /// <returns>The rounded weighted rate plus distortion.</returns>
    public static long GetCost(int rateMultiplier, int rate, long distortion)
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
