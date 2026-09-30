// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.DotDetection;

internal static class JxlDotDictionary
{
    /// <summary>
    /// Quantization level for the position.
    /// </summary>
    private const int EllipsePosQ = 2;

    /// <summary>
    /// Minimum sigma value.
    /// </summary>
    private const double EllipseMinSigma = 0.1;

    /// <summary>
    /// Maximum sigma value.
    /// </summary>
    private const double EllipseMaxSigma = 3.1;

    /// <summary>
    /// Number of quantization levels for sigma.
    /// </summary>
    private const int EllipseSigmaQ = 16;

    /// <summary>
    /// Quantization level for the angle.
    /// </summary>
    private const int EllipseAngleQ = 8;

    public static List<JxlPatchInfo> FindDotDictionary(
        Configuration configuration,
        JxlCompressParameters cparams,
        JxlImage3F opsin,
        Rectangle rect,
        JxlColorCorrelation colorCorrelation)
    {
        if (JxlOverrideHelpers.ApplyOverride(
            cparams.Dots,
            cparams.ButteraugliDistance >= MinButteraugliForDots))
        {
            JxlGaussianDetectParameters ellipseParams = default;
            ellipseParams.THigh = 0.04;
            ellipseParams.TLow = 0.02;
            ellipseParams.MaxWindowSize = 5;
            ellipseParams.MaxL2Loss = 0.005;
            ellipseParams.MaxCustomLoss = 300;
            ellipseParams.MinIntensity = 0.12;
            ellipseParams.MaxDistMeanMode = 1.0;
            ellipseParams.MaxNegativePixels = 0;
            ellipseParams.MinScore = 12;
            ellipseParams.MaxCC = 100;
            ellipseParams.PercCC = 100;

            // Read-only
            InlineArray3<double> ellipseMinIntensity = default;
            ellipseMinIntensity[0] = -0.05;
            ellipseMinIntensity[1] = 0;
            ellipseMinIntensity[2] = 0.5;

            // Read-only
            InlineArray3<double> ellipseMaxIntensity = default;
            ellipseMaxIntensity[0] = 0.05;
            ellipseMaxIntensity[1] = 1.0;
            ellipseMaxIntensity[2] = 0.4;

            // Read-only
            InlineArray3<int> ellipseIntensityQ = default;
            ellipseIntensityQ[0] = 10;
            ellipseIntensityQ[1] = 36;
            ellipseIntensityQ[2] = 10;

            JxlEllipseQuantParameters qParams = new(
                rect.Width,
                rect.Height,
                EllipsePosQ,
                EllipseMinSigma,
                EllipseMaxSigma,
                EllipseSigmaQ,
                EllipseAngleQ,
                ellipseMinIntensity,
                ellipseMaxIntensity,
                ellipseIntensityQ,
                EllipsePosQ <= 5,
                colorCorrelation.YToXRatio(0),
                colorCorrelation.YToBRatio(0));

            return JxlDotDetectionUtils.DetectGaussianEllipses(
                configuration,
                opsin,
                rect,
                ellipseParams,
                qparameters);
        }

        return [];
    }
}
