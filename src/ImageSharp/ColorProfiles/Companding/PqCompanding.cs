// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Applies the SMPTE ST 2084 perceptual-quantizer component transfer curve.
/// </summary>
/// <remarks>
/// RGB components use normalized units. Negative components use the magnitude curve with their original sign.
/// A linear value of one represents the selected normalization luminance in cd/m². It is not an output limit.
/// A nonlinear value of one represents 10000 cd/m² before normalization.
/// The normalization luminance must be positive and finite. Dividing 10000 by it must produce a finite float.
/// The signal can exceed one within the mathematical domain of the PQ curve.
/// RGB results are bounded to [-65504,65504]. NaN maps to the lower bound; infinities map to the corresponding bounds.
/// Alpha remains unchanged.
/// </remarks>
public static partial class PqCompanding
{
    /// <summary>
    /// The SMPTE ST 2084 reference luminance, in cd/m².
    /// </summary>
    internal const float ReferenceLuminance = 10000F;

    /// <summary>
    /// Converts nonlinear RGB to linear RGB and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized nonlinear colors with RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="peakLuminance">The luminance represented by linear one, in cd/m². It must permit finite PQ normalization.</param>
    public static void Expand(Span<Vector4> vectors, float peakLuminance)
    {
        ValidatePeakLuminance(peakLuminance);
        CompandingUtilities.Compand(vectors, new ExpandOperator(ReferenceLuminance / peakLuminance), PixelColorRange.Extended);
    }

    /// <summary>
    /// Converts nonlinear RGB to linear RGB and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized nonlinear color with RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="peakLuminance">The luminance represented by linear one, in cd/m². It must permit finite PQ normalization.</param>
    /// <returns>The converted RGB color with unchanged alpha.</returns>
    public static Vector4 Expand(Vector4 vector, float peakLuminance)
    {
        ValidatePeakLuminance(peakLuminance);
        return CompandingUtilities.Compand(vector, new ExpandOperator(ReferenceLuminance / peakLuminance), PixelColorRange.Extended);
    }

    /// <summary>
    /// Converts linear RGB to nonlinear RGB and retains alpha.
    /// </summary>
    /// <param name="vectors">The normalized linear colors with RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="peakLuminance">The luminance represented by linear one, in cd/m². It must permit finite PQ normalization.</param>
    public static void Compress(Span<Vector4> vectors, float peakLuminance)
    {
        ValidatePeakLuminance(peakLuminance);
        CompandingUtilities.Compand(vectors, new CompressOperator(peakLuminance * (1F / ReferenceLuminance)), PixelColorRange.Extended);
    }

    /// <summary>
    /// Converts linear RGB to nonlinear RGB and retains alpha.
    /// </summary>
    /// <param name="vector">The normalized linear color with RGB in X, Y, Z and unchanged alpha in W.</param>
    /// <param name="peakLuminance">The luminance represented by linear one, in cd/m². It must permit finite PQ normalization.</param>
    /// <returns>The converted RGB color with unchanged alpha.</returns>
    public static Vector4 Compress(Vector4 vector, float peakLuminance)
    {
        ValidatePeakLuminance(peakLuminance);
        return CompandingUtilities.Compand(vector, new CompressOperator(peakLuminance * (1F / ReferenceLuminance)), PixelColorRange.Extended);
    }

    /// <summary>
    /// Validates the luminance represented by linear one before preparing the float normalization factors.
    /// </summary>
    /// <param name="peakLuminance">The luminance represented by linear one, in cd/m².</param>
    internal static void ValidatePeakLuminance(float peakLuminance)
    {
        Guard.MustBeGreaterThan(peakLuminance, 0F, nameof(peakLuminance));
        Guard.MustBeLessThanOrEqualTo(peakLuminance, float.MaxValue, nameof(peakLuminance));

        // A positive finite luminance can still yield an infinite factor. Reject it before black can become 0 × infinity.
        Guard.MustBeLessThanOrEqualTo(ReferenceLuminance / peakLuminance, float.MaxValue, nameof(peakLuminance));
    }
}
