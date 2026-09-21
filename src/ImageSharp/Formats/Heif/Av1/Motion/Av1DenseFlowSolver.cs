// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Solves the dense inverse search flow vector of one patch.
/// </summary>
/// <remarks>
/// The patch is eight samples square, so one row of gradients is exactly one 128-bit vector of
/// sixteen-bit lanes. Every kernel here is written around that layout. Reference:
/// aom_compute_flow_at_point_c(), sobel_filter(), compute_flow_matrix(), compute_flow_vector() and
/// invert_2x2() in aom_dsp/flow_estimation/disflow.c.
/// </remarks>
internal static partial class Av1DenseFlowSolver
{
    /// <summary>
    /// The side of one flow patch.
    /// </summary>
    /// <remarks>Reference: DISFLOW_PATCH_SIZE in disflow.h.</remarks>
    public const int PatchSize = 8;

    /// <summary>
    /// The sample within a patch whose position the flow vector describes.
    /// </summary>
    /// <remarks>Reference: DISFLOW_PATCH_CENTER in disflow.h.</remarks>
    public const int PatchCenter = (PatchSize / 2) - 1;

    /// <summary>
    /// The fractional bits the gradients and the warped samples both carry.
    /// </summary>
    /// <remarks>Reference: DISFLOW_DERIV_SCALE_LOG2 in disflow.h.</remarks>
    private const int DerivativeScaleLog2 = 3;

    /// <summary>
    /// The fractional bits of the cubic interpolation kernel.
    /// </summary>
    /// <remarks>Reference: DISFLOW_INTERP_BITS in disflow.h.</remarks>
    private const int InterpolationBits = 14;

    /// <summary>
    /// The fractional bits kept between the two interpolation passes.
    /// </summary>
    /// <remarks>
    /// Six is the most the intermediate values tolerate. The worst case is samples of
    /// [0, 255, 255, 0] at a half-sample phase, whose unscaled result is 286.875; at six fractional
    /// bits that is 18,360, which a signed sixteen-bit lane holds, and at seven it would be 36,720,
    /// which it would not. Reference: the comment in compute_flow_vector() in disflow.c.
    /// </remarks>
    private const int IntermediateBits = 6;

    /// <summary>
    /// The most refinement steps one patch receives.
    /// </summary>
    /// <remarks>Reference: DISFLOW_MAX_ITR in disflow.h.</remarks>
    private const int MaximumIterations = 4;

    /// <summary>
    /// The step size threshold below which refinement stops.
    /// </summary>
    /// <remarks>Reference: DISFLOW_STEP_SIZE_THRESOLD in disflow.h.</remarks>
    private const double StepSizeThreshold = 1.0 / 8.0;

    /// <summary>
    /// Refines the flow vector of one patch.
    /// </summary>
    /// <param name="source">The source level, from its first coded sample.</param>
    /// <param name="reference">The reference level, from its first coded sample.</param>
    /// <param name="x">The patch's first column.</param>
    /// <param name="y">The patch's first row.</param>
    /// <param name="width">The level width.</param>
    /// <param name="height">The level height.</param>
    /// <param name="stride">The level row stride.</param>
    /// <param name="u">The horizontal flow, refined in place.</param>
    /// <param name="v">The vertical flow, refined in place.</param>
    /// <remarks>
    /// The gradients and the normal matrix depend only on the source, so both are computed once and
    /// the refinement then reuses them. Each step solves the two-by-two least-squares system for an
    /// increment, and the increment is clamped to two samples so that one badly conditioned patch
    /// cannot throw the field.
    /// </remarks>
    public static void Solve(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> reference,
        int x,
        int y,
        int width,
        int height,
        int stride,
        ref double u,
        ref double v)
    {
        Span<short> dx = stackalloc short[PatchSize * PatchSize];
        Span<short> dy = stackalloc short[PatchSize * PatchSize];

        // The patch's gradients are taken from the source, so one derivative direction gives the
        // horizontal gradient and the other the vertical.
        ReadOnlySpan<byte> patch = source[((y * stride) + x)..];
        SobelFilter(patch, stride, dx, derivativeHorizontal: true);
        SobelFilter(patch, stride, dy, derivativeHorizontal: false);

        ComputeNormalMatrix(dx, dy, out double m0, out double m1, out double m3);

        // The regularization added by the normal matrix keeps the determinant at one or more, so the
        // inverse always exists and needs no guard.
        double determinant = (m0 * m3) - (m1 * m1);
        double inverseDeterminant = 1 / determinant;
        double inverse0 = m3 * inverseDeterminant;
        double inverse1 = -m1 * inverseDeterminant;
        double inverse3 = m0 * inverseDeterminant;

        for (int iteration = 0; iteration < MaximumIterations; iteration++)
        {
            ComputeResidual(source, reference, width, height, stride, x, y, u, v, dx, dy, out int b0, out int b1);

            double stepU = (inverse0 * b0) + (inverse1 * b1);
            double stepV = (inverse1 * b0) + (inverse3 * b1);
            u += Math.Clamp(stepU, -2, 2);
            v += Math.Clamp(stepV, -2, 2);

            if (Math.Abs(stepU) + Math.Abs(stepV) < StepSizeThreshold)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Builds the cubic interpolation kernel of one fractional position, in Q14.
    /// </summary>
    /// <param name="fraction">The fractional position, from zero to one inclusive.</param>
    /// <param name="kernel">Receives the four taps.</param>
    /// <remarks>
    /// The position may reach exactly one through floating-point rounding of
    /// <c>value - floor(value)</c>, which still interpolates correctly.
    /// Reference: get_cubic_kernel_dbl() and get_cubic_kernel_int() in disflow.c.
    /// </remarks>
    private static void GetCubicKernel(double fraction, Span<int> kernel)
    {
        double squared = fraction * fraction;
        double cubed = squared * fraction;
        double tap0 = (-0.5 * fraction) + squared - (0.5 * cubed);
        double tap1 = 1.0 - (2.5 * squared) + (1.5 * cubed);
        double tap2 = (0.5 * fraction) + (2.0 * squared) - (1.5 * cubed);
        double tap3 = (-0.5 * squared) + (0.5 * cubed);

        kernel[0] = (int)Math.Round(tap0 * (1 << InterpolationBits), MidpointRounding.ToEven);
        kernel[1] = (int)Math.Round(tap1 * (1 << InterpolationBits), MidpointRounding.ToEven);
        kernel[2] = (int)Math.Round(tap2 * (1 << InterpolationBits), MidpointRounding.ToEven);
        kernel[3] = (int)Math.Round(tap3 * (1 << InterpolationBits), MidpointRounding.ToEven);
    }
}
