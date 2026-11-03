// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

internal sealed partial class JxlSplines
{
    /// <summary>
    /// The start of the Y field in the prepared X, Y, B and Gaussian-width coefficient block.
    /// </summary>
    private const int YCoefficientsOffset = CoefficientsPerChannel;

    /// <summary>
    /// The start of the B field in the prepared X, Y, B and Gaussian-width coefficient block.
    /// </summary>
    private const int BCoefficientsOffset = 2 * CoefficientsPerChannel;

    /// <summary>
    /// The start of the Gaussian-width field in the prepared coefficient block. Width is in coding pixels.
    /// </summary>
    private const int SigmaCoefficientsOffset = 3 * CoefficientsPerChannel;

    /// <summary>
    /// The native high-precision Gaussian tail cutoff exponent: retain intensities at least 10^-5.
    /// libjxl splines.cc ComputeSegments uses this threshold to bound the painted support.
    /// </summary>
    private const float GaussianCutoffExponent = 5F;

    /// <summary>
    /// The square-root-of-two normalization in libjxl splines.cc ContinuousIDCT.
    /// It also supplies the first angle-doubling offset in the reference cosine approximation.
    /// </summary>
    private const float RootTwo = 1.4142135623730950488F;

    /// <summary>
    /// The reciprocal square-root-of-two factor applied only to each field's DC coefficient before its channel weight.
    /// </summary>
    private const float InverseRootTwo = .7071067811865475244F;

    /// <summary>
    /// The continuous 32-point DCT frequency spacing, pi/32 radians, from libjxl ContinuousIDCT.
    /// </summary>
    private const float CosineStep = MathF.PI / CoefficientsPerChannel;

    /// <summary>
    /// The natural-log cutoff for tails below the native high-precision threshold of 10^-5.
    /// </summary>
    private static readonly float LogGaussianCutoff = MathF.Log(.1F) * GaussianCutoffExponent;

    /// <summary>
    /// Gets the libjxl splines.cc kChannelWeight values in X, Y, B and Gaussian-width order.
    /// These factors convert quantized fields to native color components and coding-pixel width.
    /// </summary>
    private static ReadOnlySpan<float> ChannelWeights => [.0042F, .075F, .07F, .3333F];

    /// <summary>
    /// Dequantizes four 32-coefficient fields and restores their base color correlation once per curve.
    /// </summary>
    /// <param name="source">The quantized X, Y, B and width fields.</param>
    /// <param name="destination">The reusable prepared 128-float block.</param>
    /// <param name="inverseQuantization">The frame's inverse quantization adjustment.</param>
    /// <param name="correlation">The base Y-to-X and Y-to-B ratios.</param>
    private static void PrepareCoefficients(ReadOnlySpan<float> source, Span<float> destination, float inverseQuantization, Vector2 correlation)
    {
        ReadOnlySpan<float> weights = ChannelWeights;
        for (int component = 0; component < 4; component++)
        {
            ReadOnlySpan<float> input = source.Slice(component * CoefficientsPerChannel, CoefficientsPerChannel);
            Span<float> output = destination.Slice(component * CoefficientsPerChannel, CoefficientsPerChannel);
            TensorPrimitives.Multiply(input, weights[component], output);

            // The first DCT coefficient has its own inverse normalization. Apply it before the channel
            // weight, preserving native single-precision multiplication order; all other factors are one.
            output[0] = (input[0] * InverseRootTwo) * weights[component];
            TensorPrimitives.Multiply(output, inverseQuantization, output);
        }

        Span<float> x = destination[..CoefficientsPerChannel];
        Span<float> y = destination.Slice(CoefficientsPerChannel, CoefficientsPerChannel);
        Span<float> b = destination.Slice(2 * CoefficientsPerChannel, CoefficientsPerChannel);
        TensorPrimitives.MultiplyAddEstimate(y, correlation.X, x, x);
        TensorPrimitives.MultiplyAddEstimate(y, correlation.Y, b, b);
    }

    /// <summary>
    /// Samples each control-point interval with the reference centripetal Catmull-Rom interpolation.
    /// </summary>
    /// <param name="controls">The validated absolute control points with no coincident neighbors.</param>
    /// <param name="destination">Exactly sixteen samples per interval followed by the final control point.</param>
    /// <param name="cancellationToken">The token checked before each control interval.</param>
    private static void Interpolate(ReadOnlySpan<Vector2> controls, Span<Vector2> destination, CancellationToken cancellationToken)
    {
        for (int segment = 0; segment < controls.Length - 1; segment++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Vector2 p1 = controls[segment];
            Vector2 p2 = controls[segment + 1];
            Vector2 p0 = segment == 0 ? p1 + (p1 - p2) : controls[segment - 1];
            Vector2 p3 = segment + 2 == controls.Length ? p2 + (p2 - p1) : controls[segment + 2];

            // Extend each endpoint along its adjacent segment to supply the missing outer control point.
            // Centripetal knot spacing is sqrt(distance), not distance or squared distance.
            float d0 = MathF.Sqrt(float.Hypot(p1.X - p0.X, p1.Y - p0.Y));
            float d1 = MathF.Sqrt(float.Hypot(p2.X - p1.X, p2.Y - p1.Y));
            float d2 = MathF.Sqrt(float.Hypot(p3.X - p2.X, p3.Y - p2.Y));
            destination[segment * InterpolationSteps] = p1;
            for (int i = 1; i < InterpolationSteps; i++)
            {
                float t = d0 + (((float)i / InterpolationSteps) * d1);
                Vector2 a0 = p0 + ((t / d0) * (p1 - p0));
                Vector2 a1 = p1 + (((t - d0) / d1) * (p2 - p1));
                Vector2 a2 = p2 + (((t - (d0 + d1)) / d2) * (p3 - p2));
                Vector2 b0 = a0 + ((t / (d0 + d1)) * (a1 - a0));
                Vector2 b1 = a1 + (((t - d0) / (d1 + d2)) * (a2 - a1));
                destination[(segment * InterpolationSteps) + i] = b0 + (((t - d0) / d1) * (b1 - b0));
            }
        }

        destination[^1] = controls[^1];
    }

    /// <summary>
    /// Advances the reference one-pixel arc-length walk, including its final fractional sample.
    /// </summary>
    /// <param name="points">The complete interpolated polyline.</param>
    /// <param name="next">The next polyline point, or length plus one after the final sample.</param>
    /// <param name="current">The previously emitted position, replaced by the next position.</param>
    /// <param name="intensity">One for a regular step, or the remaining distance for the final sample.</param>
    /// <returns>Whether a further sample was produced.</returns>
    private static bool TryGetNextPoint(ReadOnlySpan<Vector2> points, ref int next, ref Vector2 current, out float intensity)
    {
        if (next > points.Length)
        {
            intensity = 0;
            return false;
        }

        Vector2 previous = current;
        float distance = 0;
        while (next < points.Length)
        {
            Vector2 delta = points[next] - previous;
            float length = MathF.Sqrt(delta.LengthSquared());
            if (distance + length >= 1F)
            {
                current = previous + (((1F - distance) / length) * delta);
                intensity = 1F;
                return true;
            }

            distance += length;
            previous = points[next++];
        }

        // Native always emits this final point, even when its remaining distance is zero.
        // The first sample is emitted by the caller before this walk starts.
        next++;
        current = previous;
        intensity = distance;
        return true;
    }

    /// <summary>
    /// Evaluates all four continuous DCT fields at the same off-grid position.
    /// </summary>
    /// <param name="coefficients">Four consecutive prepared 32-value fields.</param>
    /// <param name="position">The continuous DCT position from zero through 31.</param>
    /// <returns>X, Y, B and Gaussian width. The fourth value is not pixel alpha.</returns>
    private static Vector4 Evaluate(ReadOnlySpan<float> coefficients, float position)
    {
        ref float source = ref MemoryMarshal.GetReference(coefficients);
        float shifted = position + .5F;
        CosineOperator cosineOperator = default;

        // The four fields share their cosine basis. Evaluate it once per frequency block, then
        // accumulate all fields. The reference float cosine avoids the runtime vector cosine
        // implementation's double-width reduction. A fixed inverse DCT cannot represent off-grid positions.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> x = Vector512<float>.Zero;
            Vector512<float> y = Vector512<float>.Zero;
            Vector512<float> b = Vector512<float>.Zero;
            Vector512<float> sigma = Vector512<float>.Zero;
            nuint blocks = CoefficientsPerChannel.Vector512Count<float>();
            for (nuint block = 0; block < blocks; block++)
            {
                nuint offset = block * (uint)Vector512<float>.Count;
                Vector512<float> frequency = Vector512.ConvertToSingle(Vector512.CreateSequence((int)offset, 1)) * Vector512.Create(CosineStep);
                Vector512<float> cosine = cosineOperator.Invoke(frequency * Vector512.Create(shifted));
                x = Vector512.MultiplyAddEstimate(Vector512.Create(RootTwo), Vector512.LoadUnsafe(ref source, offset) * cosine, x);
                y = Vector512.MultiplyAddEstimate(Vector512.Create(RootTwo), Vector512.LoadUnsafe(ref source, offset + YCoefficientsOffset) * cosine, y);
                b = Vector512.MultiplyAddEstimate(Vector512.Create(RootTwo), Vector512.LoadUnsafe(ref source, offset + BCoefficientsOffset) * cosine, b);
                sigma = Vector512.MultiplyAddEstimate(
                    Vector512.Create(RootTwo),
                    Vector512.LoadUnsafe(ref source, offset + SigmaCoefficientsOffset) * cosine,
                    sigma);
            }

            return new Vector4(Vector512.Sum(x), Vector512.Sum(y), Vector512.Sum(b), Vector512.Sum(sigma));
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> x = Vector256<float>.Zero;
            Vector256<float> y = Vector256<float>.Zero;
            Vector256<float> b = Vector256<float>.Zero;
            Vector256<float> sigma = Vector256<float>.Zero;
            nuint blocks = CoefficientsPerChannel.Vector256Count<float>();
            for (nuint block = 0; block < blocks; block++)
            {
                nuint offset = block * (uint)Vector256<float>.Count;
                Vector256<float> frequency = Vector256.ConvertToSingle(Vector256.CreateSequence((int)offset, 1)) * Vector256.Create(CosineStep);
                Vector256<float> cosine = cosineOperator.Invoke(frequency * Vector256.Create(shifted));
                x = Vector256.MultiplyAddEstimate(Vector256.Create(RootTwo), Vector256.LoadUnsafe(ref source, offset) * cosine, x);
                y = Vector256.MultiplyAddEstimate(Vector256.Create(RootTwo), Vector256.LoadUnsafe(ref source, offset + YCoefficientsOffset) * cosine, y);
                b = Vector256.MultiplyAddEstimate(Vector256.Create(RootTwo), Vector256.LoadUnsafe(ref source, offset + BCoefficientsOffset) * cosine, b);
                sigma = Vector256.MultiplyAddEstimate(
                    Vector256.Create(RootTwo),
                    Vector256.LoadUnsafe(ref source, offset + SigmaCoefficientsOffset) * cosine,
                    sigma);
            }

            return new Vector4(Vector256.Sum(x), Vector256.Sum(y), Vector256.Sum(b), Vector256.Sum(sigma));
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> x = Vector128<float>.Zero;
            Vector128<float> y = Vector128<float>.Zero;
            Vector128<float> b = Vector128<float>.Zero;
            Vector128<float> sigma = Vector128<float>.Zero;
            nuint blocks = CoefficientsPerChannel.Vector128Count<float>();
            for (nuint block = 0; block < blocks; block++)
            {
                nuint offset = block * (uint)Vector128<float>.Count;
                Vector128<float> frequency = Vector128.ConvertToSingle(Vector128.CreateSequence((int)offset, 1)) * Vector128.Create(CosineStep);
                Vector128<float> cosine = cosineOperator.Invoke(frequency * Vector128.Create(shifted));
                x = Vector128.MultiplyAddEstimate(Vector128.Create(RootTwo), Vector128.LoadUnsafe(ref source, offset) * cosine, x);
                y = Vector128.MultiplyAddEstimate(Vector128.Create(RootTwo), Vector128.LoadUnsafe(ref source, offset + YCoefficientsOffset) * cosine, y);
                b = Vector128.MultiplyAddEstimate(Vector128.Create(RootTwo), Vector128.LoadUnsafe(ref source, offset + BCoefficientsOffset) * cosine, b);
                sigma = Vector128.MultiplyAddEstimate(
                    Vector128.Create(RootTwo),
                    Vector128.LoadUnsafe(ref source, offset + SigmaCoefficientsOffset) * cosine,
                    sigma);
            }

            return new Vector4(Vector128.Sum(x), Vector128.Sum(y), Vector128.Sum(b), Vector128.Sum(sigma));
        }

        Vector4 result = Vector4.Zero;
        for (int i = 0; i < CoefficientsPerChannel; i++)
        {
            float cosine = cosineOperator.Invoke((CosineStep * i) * shifted);
            result.X = float.MultiplyAddEstimate(RootTwo, coefficients[i] * cosine, result.X);
            result.Y = float.MultiplyAddEstimate(RootTwo, coefficients[YCoefficientsOffset + i] * cosine, result.Y);
            result.Z = float.MultiplyAddEstimate(RootTwo, coefficients[BCoefficientsOffset + i] * cosine, result.Z);
            result.W = float.MultiplyAddEstimate(RootTwo, coefficients[SigmaCoefficientsOffset + i] * cosine, result.W);
        }

        return result;
    }

    /// <summary>
    /// Rounds a Gaussian's inclusive support and clips it to one visible raster axis.
    /// </summary>
    /// <param name="center">The Gaussian center on this axis.</param>
    /// <param name="distance">The nonnegative support radius.</param>
    /// <param name="length">The visible axis length.</param>
    /// <returns>The clipped start and exclusive end.</returns>
    private static Range GetRasterBounds(float center, float distance, int length)
    {
        // Native llround uses halfway-away-from-zero. Saturating conversion and a widened end+1
        // keep clipping defined even when a finite Gaussian extends far beyond the int-sized image.
        int first = Math.Clamp(int.CreateSaturating(MathF.Round(center - distance, MidpointRounding.AwayFromZero)), 0, length);
        long end = (long)int.CreateSaturating(MathF.Round(center + distance, MidpointRounding.AwayFromZero)) + 1;
        return new Range(first, (int)Math.Clamp(end, 0, length));
    }

    /// <summary>
    /// Estimates all curve work before rasterization using the reference magnitude bounds.
    /// </summary>
    /// <param name="coefficients">The original quantized coefficient fields.</param>
    /// <param name="inverseQuantization">The inverse adjustment shared by the curve.</param>
    /// <param name="correlation">The base color-correlation ratios.</param>
    /// <param name="manhattanDistance">The bounded first-delta distance.</param>
    /// <param name="total">The accumulated estimate of preceding curves, updated on success.</param>
    private void ValidateEstimatedArea(
        ReadOnlySpan<float> coefficients, float inverseQuantization, Vector2 correlation, ulong manhattanDistance, ref ulong total)
    {
        UInt128 x = 0;
        UInt128 y = 0;
        UInt128 b = 0;
        for (int i = 0; i < CoefficientsPerChannel; i++)
        {
            x += (ulong)MathF.Ceiling(inverseQuantization * MathF.Abs(coefficients[i]));
            y += (ulong)MathF.Ceiling(inverseQuantization * MathF.Abs(coefficients[YCoefficientsOffset + i]));
            b += (ulong)MathF.Ceiling(inverseQuantization * MathF.Abs(coefficients[BCoefficientsOffset + i]));
        }

        // A signed 32-bit adjustment and coefficient can produce a 2^59 term before summation.
        // Widen only this work estimate, then saturate to the reference's unsigned magnitude domain.
        // This avoids native integer-abs/add overflow without changing prepared float color arithmetic.
        x += (ulong)MathF.Ceiling(MathF.Abs(correlation.X)) * y;
        b += (ulong)MathF.Ceiling(MathF.Abs(correlation.Y)) * y;
        UInt128 maximum = UInt128.Max(y, UInt128.Max(x, b));
        ulong maximum64 = maximum > ulong.MaxValue ? ulong.MaxValue : (ulong)maximum;
        int logColor = Math.Max(1, BitOperations.Log2(maximum64) + 1);
        float weightLimit = MathF.Ceiling(MathF.Sqrt(((float)this.areaLimit / logColor) / Math.Max(1UL, manhattanDistance)));
        ulong width = 0;
        for (int i = 0; i < CoefficientsPerChannel; i++)
        {
            float weightValue = MathF.Ceiling(inverseQuantization * MathF.Abs(coefficients[SigmaCoefficientsOffset + i]));
            ulong weight = (ulong)MathF.Min(weightLimit, MathF.Max(1F, weightValue));
            width += weight * weight * (uint)logColor;
        }

        // Each bounded width term fits in ulong. Multiplication by path distance and accumulation
        // happen in UInt128, so hostile metadata cannot wrap below the supported work limit.
        UInt128 estimate = (UInt128)total + ((UInt128)width * manhattanDistance);
        if (estimate > this.areaLimit)
        {
            throw new InvalidImageContentException("The JPEG XL splines exceed the reconstruction work limit.");
        }

        total = (ulong)estimate;
    }

    /// <summary>
    /// Walks a prepared curve in native sample order and adds each Gaussian directly to its pixel rectangle.
    /// </summary>
    /// <param name="colors">The borrowed restored color planes.</param>
    /// <param name="points">The interpolated polyline in coding coordinates.</param>
    /// <param name="coefficients">The prepared X, Y, B and width coefficients.</param>
    /// <param name="cancellationToken">The token checked during painting.</param>
    private void DrawCurve(
        ReadOnlySpan<Buffer2D<float>> colors, ReadOnlySpan<Vector2> points, ReadOnlySpan<float> coefficients, CancellationToken cancellationToken)
    {
        Vector2 current = points[0];
        int next = 0;
        ulong count = 1;
        float lastDistance = 1;
        while (TryGetNextPoint(points, ref next, ref current, out float distance))
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;
            lastDistance = distance;
        }

        // Progress uses the final fractional step. Count and draw share the same arc-length walk;
        // two metadata walks avoid retaining a potentially much larger array of equally spaced samples.
        // Add each Gaussian directly to the native planes. Only the current curve's workspace remains live.
        float arcLength = (float)(count - 2) + lastDistance;
        if (arcLength <= 0F)
        {
            return;
        }

        float inverseArcLength = 1F / arcLength;
        current = points[0];
        next = 0;
        ulong index = 0;
        float intensity = 1F;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            float progress = MathF.Min(1F, (float)index * inverseArcLength);
            Vector4 value = Evaluate(coefficients, 31F * progress);
            this.DrawGaussian(colors, current, new Vector3(value.X, value.Y, value.Z), value.W, intensity, cancellationToken);
            index++;
        }
        while (TryGetNextPoint(points, ref next, ref current, out intensity));
    }

    /// <summary>
    /// Paints one Gaussian directly into its clipped native color rectangle.
    /// </summary>
    /// <param name="colors">The three retained native color planes.</param>
    /// <param name="center">The sample center in coding coordinates.</param>
    /// <param name="color">The sample's three continuous color values.</param>
    /// <param name="sigma">The continuous Gaussian width.</param>
    /// <param name="intensity">The arc-length weight of this sample.</param>
    /// <param name="cancellationToken">The token checked before each affected row.</param>
    private void DrawGaussian(
        ReadOnlySpan<Buffer2D<float>> colors, Vector2 center, Vector3 color, float sigma, float intensity, CancellationToken cancellationToken)
    {
        float inverseSigma = 1F / sigma;
        if (!float.IsFinite(sigma) || sigma == 0F || !float.IsFinite(inverseSigma) || !float.IsFinite(intensity))
        {
            // Native skips a Gaussian whose integral cannot be evaluated. This is not a pixel clamp.
            return;
        }

        float maximumColor = MathF.Max(.01F, MathF.Abs(color.X * intensity));
        maximumColor = MathF.Max(maximumColor, MathF.Abs(color.Y * intensity));
        maximumColor = MathF.Max(maximumColor, MathF.Abs(color.Z * intensity));
        float maximumDistance = MathF.Sqrt(-2F * sigma * sigma * (LogGaussianCutoff - MathF.Log(maximumColor)));
        (int firstX, int count) = GetRasterBounds(center.X, maximumDistance, this.codingSize.Width).GetOffsetAndLength(this.codingSize.Width);
        (int firstY, int rows) = GetRasterBounds(center.Y, maximumDistance, this.codingSize.Height).GetOffsetAndLength(this.codingSize.Height);
        if (count == 0 || rows == 0)
        {
            // A valid curve can lie outside the visible image. Discard an empty clipped support
            // before visiting rows, as native ComputeSegments and DrawSegment do.
            return;
        }

        float scaledIntensity = .25F * sigma * intensity;
        for (int y = firstY; y < firstY + rows; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Span<float> rowX = colors[0].DangerousGetRowSpan(y).Slice(firstX, count);
            Span<float> rowY = colors[1].DangerousGetRowSpan(y).Slice(firstX, count);
            Span<float> rowB = colors[2].DangerousGetRowSpan(y).Slice(firstX, count);
            ref float xStart = ref MemoryMarshal.GetReference(rowX);
            ref float yStart = ref MemoryMarshal.GetReference(rowY);
            ref float bStart = ref MemoryMarshal.GetReference(rowB);
            float dy = y - center.Y;
            float dySquared = dy * dy;
            GaussianOperator operation = new(center.X, dySquared, inverseSigma, scaledIntensity);
            int i = 0;

            // Independent lanes paint consecutive columns. The complete clipped spans prove every
            // unaligned load/store. Descending widths leave only scalar tails, without padded pixel reads.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint blocks = count.Vector512Count<float>();
                for (nuint block = 0; block < blocks; block++, i += Vector512<float>.Count)
                {
                    Vector512<float> columns = Vector512.ConvertToSingle(Vector512.CreateSequence(firstX + i, 1));
                    Vector512<float> value = operation.Invoke(columns);

                    Vector512<float> x = Vector512.MultiplyAddEstimate(Vector512.Create(color.X), value, Vector512.LoadUnsafe(ref xStart, (nuint)i));
                    Vector512<float> yValue = Vector512.MultiplyAddEstimate(Vector512.Create(color.Y), value, Vector512.LoadUnsafe(ref yStart, (nuint)i));
                    Vector512<float> b = Vector512.MultiplyAddEstimate(Vector512.Create(color.Z), value, Vector512.LoadUnsafe(ref bStart, (nuint)i));
                    x.StoreUnsafe(ref xStart, (nuint)i);
                    yValue.StoreUnsafe(ref yStart, (nuint)i);
                    b.StoreUnsafe(ref bStart, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint blocks = (count - i).Vector256Count<float>();
                for (nuint block = 0; block < blocks; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> columns = Vector256.ConvertToSingle(Vector256.CreateSequence(firstX + i, 1));
                    Vector256<float> value = operation.Invoke(columns);

                    Vector256<float> x = Vector256.MultiplyAddEstimate(Vector256.Create(color.X), value, Vector256.LoadUnsafe(ref xStart, (nuint)i));
                    Vector256<float> yValue = Vector256.MultiplyAddEstimate(Vector256.Create(color.Y), value, Vector256.LoadUnsafe(ref yStart, (nuint)i));
                    Vector256<float> b = Vector256.MultiplyAddEstimate(Vector256.Create(color.Z), value, Vector256.LoadUnsafe(ref bStart, (nuint)i));
                    x.StoreUnsafe(ref xStart, (nuint)i);
                    yValue.StoreUnsafe(ref yStart, (nuint)i);
                    b.StoreUnsafe(ref bStart, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint blocks = (count - i).Vector128Count<float>();
                for (nuint block = 0; block < blocks; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> columns = Vector128.ConvertToSingle(Vector128.CreateSequence(firstX + i, 1));
                    Vector128<float> value = operation.Invoke(columns);

                    Vector128<float> x = Vector128.MultiplyAddEstimate(Vector128.Create(color.X), value, Vector128.LoadUnsafe(ref xStart, (nuint)i));
                    Vector128<float> yValue = Vector128.MultiplyAddEstimate(Vector128.Create(color.Y), value, Vector128.LoadUnsafe(ref yStart, (nuint)i));
                    Vector128<float> b = Vector128.MultiplyAddEstimate(Vector128.Create(color.Z), value, Vector128.LoadUnsafe(ref bStart, (nuint)i));
                    x.StoreUnsafe(ref xStart, (nuint)i);
                    yValue.StoreUnsafe(ref yStart, (nuint)i);
                    b.StoreUnsafe(ref bStart, (nuint)i);
                }
            }

            for (; i < count; i++)
            {
                float value = operation.Invoke(firstX + i);
                rowX[i] = float.MultiplyAddEstimate(color.X, value, rowX[i]);
                rowY[i] = float.MultiplyAddEstimate(color.Y, value, rowY[i]);
                rowB[i] = float.MultiplyAddEstimate(color.Z, value, rowB[i]);
            }
        }
    }
}
