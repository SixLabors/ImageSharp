// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Solves the dense inverse search flow vector of one patch.
/// </summary>
/// <remarks>
/// <para>
/// A patch is eight samples square, so one row of gradients or of interpolated values is exactly one 128-bit vector. For this reason, the traversals
/// have a 128-bit path and a scalar path, but no wider path. A 256-bit vector spans two patch rows. The gradient filter and the interpolation both
/// read a row on each side of the row that they produce. Thus packed rows need cross-row shifts that cost more than they save.
/// </para>
/// <para>
/// Every kernel reads at most one sample before the patch and two after it. The warp position is clamped only to the border of the level, not to
/// the level itself. Thus a caller passes the whole storage of the level and the index of its first coded sample.
/// <see cref="Av1ImagePyramid.GetSamples"/> and <see cref="Av1ImagePyramid.Level.Origin"/> supply these values. The border must be at least
/// <see cref="Av1ImagePyramid.Padding"/> samples wide. Every read is then a non-negative index into the span, so the span bounds are the only bounds.
/// </para>
/// </remarks>
internal static partial class Av1DenseFlowSolver
{
    /// <summary>
    /// The side of one flow patch.
    /// </summary>
    public const int PatchSize = 8;

    /// <summary>
    /// The sample within a patch whose position the flow vector describes.
    /// </summary>
    public const int PatchCenter = (PatchSize / 2) - 1;

    /// <summary>
    /// The fractional bits that the gradients and the warped samples both carry.
    /// </summary>
    private const int DerivativeScaleLog2 = 3;

    /// <summary>
    /// The fractional bits of the cubic interpolation kernel.
    /// </summary>
    private const int InterpolationBits = 14;

    /// <summary>
    /// The fractional bits kept between the two interpolation passes.
    /// </summary>
    /// <remarks>
    /// Six is the most that the intermediate values tolerate. The worst case is the samples [0, 255, 255, 0] at a half-sample phase. Its unscaled
    /// result is 286.875. At six fractional bits, the value is 18360, which fits in a signed sixteen-bit value. At seven fractional bits, the value
    /// is 36720, which does not fit.
    /// </remarks>
    private const int IntermediateBits = 6;

    /// <summary>
    /// The most refinement steps that one patch receives.
    /// </summary>
    private const int MaximumIterations = 4;

    /// <summary>
    /// The step size below which refinement stops.
    /// </summary>
    private const double StepSizeThreshold = 1.0 / 8.0;

    /// <summary>
    /// The largest increment that one refinement step can apply, in samples.
    /// </summary>
    /// <remarks>
    /// The step is clamped to this value. Thus one patch with a badly conditioned normal matrix cannot move the field far.
    /// </remarks>
    private const double MaximumStep = 2;

    /// <summary>
    /// Refines the flow vector of one patch.
    /// </summary>
    /// <param name="source">The whole storage of the source level, including its border.</param>
    /// <param name="reference">The whole storage of the reference level, including its border.</param>
    /// <param name="origin">The index of the first coded sample of each level.</param>
    /// <param name="x">The first column of the patch.</param>
    /// <param name="y">The first row of the patch.</param>
    /// <param name="width">The width of the level.</param>
    /// <param name="height">The height of the level.</param>
    /// <param name="stride">The row stride of the level.</param>
    /// <param name="u">The horizontal flow, refined in place.</param>
    /// <param name="v">The vertical flow, refined in place.</param>
    /// <remarks>
    /// The gradients and the normal matrix depend only on the source. Thus the method computes both once, and the refinement reuses them. Each step
    /// solves the two-by-two least-squares system for an increment and applies the clamped increment.
    /// </remarks>
    public static void Solve(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> reference,
        int origin,
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

        // The gradients of a patch come from the source. One direction of the separable filter gives the horizontal gradient, and the other
        // gives the vertical gradient.
        int patch = origin + (y * stride) + x;
        Sobel<HorizontalGradientOperator>.Apply(source, patch, stride, dx);
        Sobel<VerticalGradientOperator>.Apply(source, patch, stride, dy);

        ComputeNormalMatrix(dx, dy, out double m0, out double m1, out double m3);

        // The regularization that the normal matrix adds keeps the determinant at one or more, so the inverse always exists and needs no guard.
        double determinant = (m0 * m3) - (m1 * m1);
        double inverseDeterminant = 1 / determinant;
        double inverse0 = m3 * inverseDeterminant;
        double inverse1 = -m1 * inverseDeterminant;
        double inverse3 = m0 * inverseDeterminant;

        for (int iteration = 0; iteration < MaximumIterations; iteration++)
        {
            Warp<CubicOperator>.ComputeResidual(
                source, reference, origin, width, height, stride, x, y, u, v, dx, dy, out int b0, out int b1);

            double stepU = (inverse0 * b0) + (inverse1 * b1);
            double stepV = (inverse1 * b0) + (inverse3 * b1);
            u += Math.Clamp(stepU, -MaximumStep, MaximumStep);
            v += Math.Clamp(stepV, -MaximumStep, MaximumStep);

            if (Math.Abs(stepU) + Math.Abs(stepV) < StepSizeThreshold)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Builds the cubic interpolation kernel of one fractional position, in Q14.
    /// </summary>
    /// <param name="fraction">The fractional position, from zero through one inclusive.</param>
    /// <param name="kernel">Receives the four taps.</param>
    /// <remarks>
    /// The floating-point rounding of <c>value - floor(value)</c> can make the position exactly one. The kernel still interpolates correctly there.
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
