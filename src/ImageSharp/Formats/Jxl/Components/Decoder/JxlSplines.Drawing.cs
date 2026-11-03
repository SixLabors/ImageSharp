// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

internal sealed partial class JxlSplines
{
    private const int YCoefficientsOffset = CoefficientsPerChannel;
    private const int BCoefficientsOffset = 2 * CoefficientsPerChannel;
    private const int SigmaCoefficientsOffset = 3 * CoefficientsPerChannel;
    private const float GaussianCutoffExponent = 5F;
    private const float RootTwo = 1.4142135623730950488F;
    private const float InverseRootTwo = .7071067811865475244F;
    private const float CosineStep = MathF.PI / CoefficientsPerChannel;
    private const float GaussianIntegralOffset = .353553391F;
    private const float ErfCoefficient0 = .0777394369F;
    private const float ErfCoefficient1 = .000205260015F;
    private const float ErfCoefficient2 = .232120216F;
    private const float ErfCoefficient3 = .277820801F;

    /// <summary>
    /// The natural-log cutoff for tails below the native high-precision threshold of 10^-5.
    /// </summary>
    private static readonly float LogGaussianCutoff = MathF.Log(.1F) * GaussianCutoffExponent;

    /// <summary>
    /// Gets the spline quantization weights in X, Y, B and Gaussian-width order.
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

            // The endpoint extensions replace native's copied and padded control-point array.
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
    /// Evaluates the reference error-function approximation for Gaussian integration.
    /// </summary>
    /// <param name="value">The signed normalized integration endpoint.</param>
    /// <returns>The approximate error function with the input sign.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Erf(float value)
    {
        // Both references use this fitted fourth-power reciprocal polynomial. The runtime and the
        // existing shared numeric helpers have no error-function operation to reuse.
        float absolute = MathF.Abs(value);
        float denominator = float.MultiplyAddEstimate(absolute, ErfCoefficient0, ErfCoefficient1);
        denominator = float.MultiplyAddEstimate(denominator, absolute, ErfCoefficient2);
        denominator = float.MultiplyAddEstimate(denominator, absolute, ErfCoefficient3);
        denominator = float.MultiplyAddEstimate(denominator, absolute, 1F);
        float reciprocal = 1F / (denominator * denominator);
        return MathF.CopySign(float.MultiplyAddEstimate(-reciprocal, reciprocal, 1F), value);
    }

    /// <inheritdoc cref="Erf(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Erf(Vector128<float> value)
    {
        Vector128<float> absolute = Vector128.Abs(value);
        Vector128<float> denominator = Vector128.MultiplyAddEstimate(absolute, Vector128.Create(ErfCoefficient0), Vector128.Create(ErfCoefficient1));
        denominator = Vector128.MultiplyAddEstimate(denominator, absolute, Vector128.Create(ErfCoefficient2));
        denominator = Vector128.MultiplyAddEstimate(denominator, absolute, Vector128.Create(ErfCoefficient3));
        denominator = Vector128.MultiplyAddEstimate(denominator, absolute, Vector128<float>.One);
        Vector128<float> reciprocal = Vector128<float>.One / (denominator * denominator);
        return Vector128.CopySign(Vector128.MultiplyAddEstimate(-reciprocal, reciprocal, Vector128<float>.One), value);
    }

    /// <inheritdoc cref="Erf(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> Erf(Vector256<float> value)
    {
        Vector256<float> absolute = Vector256.Abs(value);
        Vector256<float> denominator = Vector256.MultiplyAddEstimate(absolute, Vector256.Create(ErfCoefficient0), Vector256.Create(ErfCoefficient1));
        denominator = Vector256.MultiplyAddEstimate(denominator, absolute, Vector256.Create(ErfCoefficient2));
        denominator = Vector256.MultiplyAddEstimate(denominator, absolute, Vector256.Create(ErfCoefficient3));
        denominator = Vector256.MultiplyAddEstimate(denominator, absolute, Vector256<float>.One);
        Vector256<float> reciprocal = Vector256<float>.One / (denominator * denominator);
        return Vector256.CopySign(Vector256.MultiplyAddEstimate(-reciprocal, reciprocal, Vector256<float>.One), value);
    }

    /// <inheritdoc cref="Erf(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> Erf(Vector512<float> value)
    {
        Vector512<float> absolute = Vector512.Abs(value);
        Vector512<float> denominator = Vector512.MultiplyAddEstimate(absolute, Vector512.Create(ErfCoefficient0), Vector512.Create(ErfCoefficient1));
        denominator = Vector512.MultiplyAddEstimate(denominator, absolute, Vector512.Create(ErfCoefficient2));
        denominator = Vector512.MultiplyAddEstimate(denominator, absolute, Vector512.Create(ErfCoefficient3));
        denominator = Vector512.MultiplyAddEstimate(denominator, absolute, Vector512<float>.One);
        Vector512<float> reciprocal = Vector512<float>.One / (denominator * denominator);
        return Vector512.CopySign(Vector512.MultiplyAddEstimate(-reciprocal, reciprocal, Vector512<float>.One), value);
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
        // Painting full planes also removes native's retained Gaussian and per-row index collections.
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

    /// <summary>
    /// Evaluates the reference float cosine for the bounded continuous spline DCT arguments.
    /// </summary>
    /// <remarks>
    /// Arguments range from zero through (31 * pi / 32) * 31.5 radians. This is the float-only
    /// FastCosf algorithm in libjxl base/fast_math-inl.h and jxl-rs util/fast_math.rs.
    /// The runtime cosine widens float lanes to double; this kernel retains the reference float arithmetic.
    /// </remarks>
    private readonly struct CosineOperator : TensorPrimitives_.IUnaryOperator
    {
        private const float Period = 2F * MathF.PI;
        private const float InversePeriod = .5F / MathF.PI;
        private const float HalfPi = MathF.PI / 2F;
        private const float QuarterAngle = .25F;
        private const float QuarticCoefficient = .06960438F;
        private const float QuadraticCoefficient = -.84087373F;
        private const float ConstantCoefficient = 1.68179268F;

        /// <summary>
        /// Evaluates one bounded angle with range folding and two angle doublings.
        /// </summary>
        /// <param name="value">The nonnegative spline DCT angle.</param>
        /// <returns>The reference cosine approximation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            float reduced = value - (MathF.Floor(value * InversePeriod) * Period);
            float folded = MathF.Min(reduced, Period - reduced);
            bool negative = folded >= HalfPi;
            float quarter = (negative ? MathF.PI - folded : folded) * QuarterAngle;
            float squared = quarter * quarter;
            float fourth = squared * squared;
            float polynomial = float.MultiplyAddEstimate(
                fourth,
                QuarticCoefficient,
                float.MultiplyAddEstimate(squared, QuadraticCoefficient, ConstantCoefficient));

            float doubled = float.MultiplyAddEstimate(polynomial, polynomial, -RootTwo);
            float result = float.MultiplyAddEstimate(doubled, doubled, -1F);
            return negative ? -result : result;
        }

        /// <inheritdoc cref="Invoke(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Each lane is one frequency. Fold it to [0, pi/2], evaluate the fitted quartic at
            // one quarter of the angle, then double twice and restore the folded sign.
            Vector128<float> reduced = value - (Vector128.Floor(value * Vector128.Create(InversePeriod)) * Vector128.Create(Period));
            Vector128<float> folded = Vector128.Min(reduced, Vector128.Create(Period) - reduced);
            Vector128<float> negative = Vector128.GreaterThanOrEqual(folded, Vector128.Create(HalfPi));
            Vector128<float> quarter = Vector128.ConditionalSelect(negative, Vector128.Create(MathF.PI) - folded, folded) * Vector128.Create(QuarterAngle);
            Vector128<float> squared = quarter * quarter;
            Vector128<float> fourth = squared * squared;
            Vector128<float> polynomial = Vector128.MultiplyAddEstimate(
                fourth,
                Vector128.Create(QuarticCoefficient),
                Vector128.MultiplyAddEstimate(squared, Vector128.Create(QuadraticCoefficient), Vector128.Create(ConstantCoefficient)));

            Vector128<float> doubled = Vector128.MultiplyAddEstimate(polynomial, polynomial, Vector128.Create(-RootTwo));
            Vector128<float> result = Vector128.MultiplyAddEstimate(doubled, doubled, -Vector128<float>.One);
            return Vector128.ConditionalSelect(negative, -result, result);
        }

        /// <inheritdoc cref="Invoke(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Each lane is one frequency. Fold it to [0, pi/2], evaluate the fitted quartic at
            // one quarter of the angle, then double twice and restore the folded sign.
            Vector256<float> reduced = value - (Vector256.Floor(value * Vector256.Create(InversePeriod)) * Vector256.Create(Period));
            Vector256<float> folded = Vector256.Min(reduced, Vector256.Create(Period) - reduced);
            Vector256<float> negative = Vector256.GreaterThanOrEqual(folded, Vector256.Create(HalfPi));
            Vector256<float> quarter = Vector256.ConditionalSelect(negative, Vector256.Create(MathF.PI) - folded, folded) * Vector256.Create(QuarterAngle);
            Vector256<float> squared = quarter * quarter;
            Vector256<float> fourth = squared * squared;
            Vector256<float> polynomial = Vector256.MultiplyAddEstimate(
                fourth,
                Vector256.Create(QuarticCoefficient),
                Vector256.MultiplyAddEstimate(squared, Vector256.Create(QuadraticCoefficient), Vector256.Create(ConstantCoefficient)));

            Vector256<float> doubled = Vector256.MultiplyAddEstimate(polynomial, polynomial, Vector256.Create(-RootTwo));
            Vector256<float> result = Vector256.MultiplyAddEstimate(doubled, doubled, -Vector256<float>.One);
            return Vector256.ConditionalSelect(negative, -result, result);
        }

        /// <inheritdoc cref="Invoke(float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Each lane is one frequency. Fold it to [0, pi/2], evaluate the fitted quartic at
            // one quarter of the angle, then double twice and restore the folded sign.
            Vector512<float> reduced = value - (Vector512.Floor(value * Vector512.Create(InversePeriod)) * Vector512.Create(Period));
            Vector512<float> folded = Vector512.Min(reduced, Vector512.Create(Period) - reduced);
            Vector512<float> negative = Vector512.GreaterThanOrEqual(folded, Vector512.Create(HalfPi));
            Vector512<float> quarter = Vector512.ConditionalSelect(negative, Vector512.Create(MathF.PI) - folded, folded) * Vector512.Create(QuarterAngle);
            Vector512<float> squared = quarter * quarter;
            Vector512<float> fourth = squared * squared;
            Vector512<float> polynomial = Vector512.MultiplyAddEstimate(
                fourth,
                Vector512.Create(QuarticCoefficient),
                Vector512.MultiplyAddEstimate(squared, Vector512.Create(QuadraticCoefficient), Vector512.Create(ConstantCoefficient)));

            Vector512<float> doubled = Vector512.MultiplyAddEstimate(polynomial, polynomial, Vector512.Create(-RootTwo));
            Vector512<float> result = Vector512.MultiplyAddEstimate(doubled, doubled, -Vector512<float>.One);
            return Vector512.ConditionalSelect(negative, -result, result);
        }
    }

    /// <summary>
    /// Calculates one Gaussian's integrated intensity at independent horizontal pixel positions.
    /// </summary>
    /// <remarks>
    /// The three color planes consume the same intensity register. Parameters are prepared once per
    /// painted row; no intermediate intensity row or separate tensor traversal is required.
    /// </remarks>
    private readonly struct GaussianOperator : TensorPrimitives_.IUnaryOperator
    {
        private readonly float centerX;
        private readonly float dySquared;
        private readonly float inverseSigma;
        private readonly float scaledIntensity;

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianOperator"/> struct for one painted row.
        /// </summary>
        /// <param name="centerX">The horizontal Gaussian center in coding coordinates.</param>
        /// <param name="dySquared">The row's squared vertical distance from the center.</param>
        /// <param name="inverseSigma">The reciprocal Gaussian width.</param>
        /// <param name="scaledIntensity">Sigma times one quarter of the sample's arc-length weight.</param>
        public GaussianOperator(float centerX, float dySquared, float inverseSigma, float scaledIntensity)
        {
            this.centerX = centerX;
            this.dySquared = dySquared;
            this.inverseSigma = inverseSigma;
            this.scaledIntensity = scaledIntensity;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            float dx = value - this.centerX;
            float distance = MathF.Sqrt(float.MultiplyAddEstimate(dx, dx, this.dySquared));
            float first = float.MultiplyAddEstimate(distance, .5F, GaussianIntegralOffset) * this.inverseSigma;
            float second = float.MultiplyAddEstimate(distance, .5F, -GaussianIntegralOffset) * this.inverseSigma;
            float integral = Erf(first) - Erf(second);
            return this.scaledIntensity * (integral * integral);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Lanes are consecutive pixels. Broadcast row parameters, evaluate the scalar integral
            // independently, and retain its intensity for all three color-plane updates.
            Vector128<float> dx = value - Vector128.Create(this.centerX);
            Vector128<float> distance = Vector128.Sqrt(Vector128.MultiplyAddEstimate(dx, dx, Vector128.Create(this.dySquared)));
            Vector128<float> first = Vector128.MultiplyAddEstimate(
                distance,
                Vector128.Create(.5F),
                Vector128.Create(GaussianIntegralOffset)) * Vector128.Create(this.inverseSigma);

            Vector128<float> second = Vector128.MultiplyAddEstimate(
                distance,
                Vector128.Create(.5F),
                Vector128.Create(-GaussianIntegralOffset)) * Vector128.Create(this.inverseSigma);

            Vector128<float> integral = Erf(first) - Erf(second);
            return Vector128.Create(this.scaledIntensity) * (integral * integral);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Lanes are consecutive pixels. Broadcast row parameters, evaluate the scalar integral
            // independently, and retain its intensity for all three color-plane updates.
            Vector256<float> dx = value - Vector256.Create(this.centerX);
            Vector256<float> distance = Vector256.Sqrt(Vector256.MultiplyAddEstimate(dx, dx, Vector256.Create(this.dySquared)));
            Vector256<float> first = Vector256.MultiplyAddEstimate(
                distance,
                Vector256.Create(.5F),
                Vector256.Create(GaussianIntegralOffset)) * Vector256.Create(this.inverseSigma);

            Vector256<float> second = Vector256.MultiplyAddEstimate(
                distance,
                Vector256.Create(.5F),
                Vector256.Create(-GaussianIntegralOffset)) * Vector256.Create(this.inverseSigma);

            Vector256<float> integral = Erf(first) - Erf(second);
            return Vector256.Create(this.scaledIntensity) * (integral * integral);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Lanes are consecutive pixels. Broadcast row parameters, evaluate the scalar integral
            // independently, and retain its intensity for all three color-plane updates.
            Vector512<float> dx = value - Vector512.Create(this.centerX);
            Vector512<float> distance = Vector512.Sqrt(Vector512.MultiplyAddEstimate(dx, dx, Vector512.Create(this.dySquared)));
            Vector512<float> first = Vector512.MultiplyAddEstimate(
                distance,
                Vector512.Create(.5F),
                Vector512.Create(GaussianIntegralOffset)) * Vector512.Create(this.inverseSigma);

            Vector512<float> second = Vector512.MultiplyAddEstimate(
                distance,
                Vector512.Create(.5F),
                Vector512.Create(-GaussianIntegralOffset)) * Vector512.Create(this.inverseSigma);

            Vector512<float> integral = Erf(first) - Erf(second);
            return Vector512.Create(this.scaledIntensity) * (integral * integral);
        }
    }
}
