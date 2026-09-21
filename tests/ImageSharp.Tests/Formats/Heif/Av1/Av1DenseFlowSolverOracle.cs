// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Refines one patch's flow vector with a direct transcription of the reference decoder.
/// </summary>
/// <remarks>
/// Written as the plainest possible form of the reference rather than as fast code, so that it is
/// easy to compare with the C source line by line. Reference: aom_compute_flow_at_point_c(),
/// sobel_filter(), compute_flow_matrix(), invert_2x2(), compute_flow_vector(),
/// get_cubic_kernel_int() and get_cubic_value_int() in aom_dsp/flow_estimation/disflow.c.
/// </remarks>
internal static class Av1DenseFlowSolverOracle
{
    private const int PatchSize = 8;
    private const int DerivativeScaleLog2 = 3;
    private const int InterpolationBits = 14;
    private const int MaximumIterations = 4;
    private const double StepSizeThreshold = 1.0 / 8.0;

    /// <summary>
    /// Refines the flow vector of one patch.
    /// </summary>
    /// <param name="source">The source plane, from its first coded sample.</param>
    /// <param name="reference">The reference plane, from its first coded sample.</param>
    /// <param name="origin">The index of the first coded sample within each plane.</param>
    /// <param name="x">The patch's first column.</param>
    /// <param name="y">The patch's first row.</param>
    /// <param name="width">The plane width.</param>
    /// <param name="height">The plane height.</param>
    /// <param name="stride">The plane row stride.</param>
    /// <param name="u">The horizontal flow, refined in place.</param>
    /// <param name="v">The vertical flow, refined in place.</param>
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
        short[] dx = new short[PatchSize * PatchSize];
        short[] dy = new short[PatchSize * PatchSize];
        int patch = origin + (y * stride) + x;
        SobelFilter(source, patch, stride, dx, derivative: true);
        SobelFilter(source, patch, stride, dy, derivative: false);

        int[] m = new int[4];
        for (int i = 0; i < PatchSize; i++)
        {
            for (int j = 0; j < PatchSize; j++)
            {
                m[0] += dx[(i * PatchSize) + j] * dx[(i * PatchSize) + j];
                m[1] += dx[(i * PatchSize) + j] * dy[(i * PatchSize) + j];
                m[3] += dy[(i * PatchSize) + j] * dy[(i * PatchSize) + j];
            }
        }

        m[0] += 1;
        m[3] += 1;
        m[2] = m[1];

        double determinant = ((double)m[0] * m[3]) - ((double)m[1] * m[2]);
        double inverseDeterminant = 1 / determinant;
        double inverse0 = m[3] * inverseDeterminant;
        double inverse1 = -m[1] * inverseDeterminant;
        double inverse2 = -m[2] * inverseDeterminant;
        double inverse3 = m[0] * inverseDeterminant;

        for (int iteration = 0; iteration < MaximumIterations; iteration++)
        {
            ComputeFlowVector(source, reference, origin, width, height, stride, x, y, u, v, dx, dy, out int b0, out int b1);

            double stepU = (inverse0 * b0) + (inverse1 * b1);
            double stepV = (inverse2 * b0) + (inverse3 * b1);
            u += Math.Clamp(stepU, -2, 2);
            v += Math.Clamp(stepV, -2, 2);

            if (Math.Abs(stepU) + Math.Abs(stepV) < StepSizeThreshold)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Fills one patch's gradients in one direction.
    /// </summary>
    /// <param name="plane">The plane samples.</param>
    /// <param name="patch">The index of the patch's first sample.</param>
    /// <param name="stride">The plane row stride.</param>
    /// <param name="destination">Receives the gradients.</param>
    /// <param name="derivative">Whether the derivative runs along the rows.</param>
    private static void SobelFilter(
        ReadOnlySpan<byte> plane,
        int patch,
        int stride,
        Span<short> destination,
        bool derivative)
    {
        ReadOnlySpan<short> derivativeTaps = [1, 0, -1];
        ReadOnlySpan<short> smoothingTaps = [1, 2, 1];
        short[] intermediate = new short[PatchSize * (PatchSize + 2)];

        ReadOnlySpan<short> horizontal = derivative ? derivativeTaps : smoothingTaps;
        for (int y = -1; y < PatchSize + 1; y++)
        {
            for (int x = 0; x < PatchSize; x++)
            {
                int sum = 0;
                for (int tap = 0; tap < 3; tap++)
                {
                    sum += horizontal[tap] * plane[patch + (y * stride) + x + tap - 1];
                }

                intermediate[((y + 1) * PatchSize) + x] = (short)sum;
            }
        }

        ReadOnlySpan<short> vertical = derivative ? smoothingTaps : derivativeTaps;
        for (int y = 0; y < PatchSize; y++)
        {
            for (int x = 0; x < PatchSize; x++)
            {
                int sum = 0;
                for (int tap = 0; tap < 3; tap++)
                {
                    sum += vertical[tap] * intermediate[((y + tap) * PatchSize) + x];
                }

                destination[(y * PatchSize) + x] = (short)sum;
            }
        }
    }

    /// <summary>
    /// Accumulates the gradient-weighted error against the warped reference.
    /// </summary>
    /// <param name="source">The source plane.</param>
    /// <param name="reference">The reference plane.</param>
    /// <param name="origin">The index of the first coded sample.</param>
    /// <param name="width">The plane width.</param>
    /// <param name="height">The plane height.</param>
    /// <param name="stride">The plane row stride.</param>
    /// <param name="x">The patch's first column.</param>
    /// <param name="y">The patch's first row.</param>
    /// <param name="u">The horizontal flow.</param>
    /// <param name="v">The vertical flow.</param>
    /// <param name="dx">The horizontal gradients.</param>
    /// <param name="dy">The vertical gradients.</param>
    /// <param name="b0">Receives the horizontal component.</param>
    /// <param name="b1">Receives the vertical component.</param>
    private static void ComputeFlowVector(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> reference,
        int origin,
        int width,
        int height,
        int stride,
        int x,
        int y,
        double u,
        double v,
        ReadOnlySpan<short> dx,
        ReadOnlySpan<short> dy,
        out int b0,
        out int b1)
    {
        b0 = 0;
        b1 = 0;
        int integerU = (int)Math.Floor(u);
        int integerV = (int)Math.Floor(v);
        int[] horizontalKernel = new int[4];
        int[] verticalKernel = new int[4];
        GetCubicKernel(u - Math.Floor(u), horizontalKernel);
        GetCubicKernel(v - Math.Floor(v), verticalKernel);

        int[] intermediate = new int[PatchSize * (PatchSize + 3)];
        int x0 = Math.Clamp(x + integerU, -9, width);
        int y0 = Math.Clamp(y + integerV, -9, height);

        for (int i = -1; i < PatchSize + 2; i++)
        {
            int warpedY = y0 + i;
            for (int j = 0; j < PatchSize; j++)
            {
                int warpedX = x0 + j;
                int[] taps =
                [
                    reference[origin + (warpedY * stride) + warpedX - 1],
                    reference[origin + (warpedY * stride) + warpedX],
                    reference[origin + (warpedY * stride) + warpedX + 1],
                    reference[origin + (warpedY * stride) + warpedX + 2]
                ];

                intermediate[((i + 1) * PatchSize) + j] =
                    RoundShift(GetCubicValue(taps, horizontalKernel), InterpolationBits - 6);
            }
        }

        int roundBits = InterpolationBits + 6 - DerivativeScaleLog2;
        for (int i = 0; i < PatchSize; i++)
        {
            for (int j = 0; j < PatchSize; j++)
            {
                int start = ((i + 1) * PatchSize) + j;
                int[] taps =
                [
                    intermediate[start - PatchSize],
                    intermediate[start],
                    intermediate[start + PatchSize],
                    intermediate[start + (2 * PatchSize)]
                ];

                int warped = RoundShift(GetCubicValue(taps, verticalKernel), roundBits);
                int sourceSample = source[origin + ((y + i) * stride) + x + j] << DerivativeScaleLog2;
                int difference = warped - sourceSample;
                b0 += dx[(i * PatchSize) + j] * difference;
                b1 += dy[(i * PatchSize) + j] * difference;
            }
        }
    }

    /// <summary>
    /// Builds the cubic interpolation kernel of one fractional position.
    /// </summary>
    /// <param name="fraction">The fractional position.</param>
    /// <param name="kernel">Receives the four taps.</param>
    private static void GetCubicKernel(double fraction, Span<int> kernel)
    {
        double squared = fraction * fraction;
        double cubed = squared * fraction;
        kernel[0] = (int)Math.Round(((-0.5 * fraction) + squared - (0.5 * cubed)) * (1 << InterpolationBits), MidpointRounding.ToEven);
        kernel[1] = (int)Math.Round((1.0 - (2.5 * squared) + (1.5 * cubed)) * (1 << InterpolationBits), MidpointRounding.ToEven);
        kernel[2] = (int)Math.Round(((0.5 * fraction) + (2.0 * squared) - (1.5 * cubed)) * (1 << InterpolationBits), MidpointRounding.ToEven);
        kernel[3] = (int)Math.Round(((-0.5 * squared) + (0.5 * cubed)) * (1 << InterpolationBits), MidpointRounding.ToEven);
    }

    /// <summary>
    /// Applies a four-tap kernel to four values.
    /// </summary>
    /// <param name="taps">The four values.</param>
    /// <param name="kernel">The four taps.</param>
    /// <returns>The weighted sum.</returns>
    private static int GetCubicValue(ReadOnlySpan<int> taps, ReadOnlySpan<int> kernel)
        => (kernel[0] * taps[0]) + (kernel[1] * taps[1]) + (kernel[2] * taps[2]) + (kernel[3] * taps[3]);

    /// <summary>
    /// Applies a rounding right shift.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="bits">The shift.</param>
    /// <returns>The shifted value.</returns>
    private static int RoundShift(int value, int bits) => (value + (1 << (bits - 1))) >> bits;
}
