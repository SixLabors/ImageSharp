// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal static class JxlSplineUtils
{
    /// <summary>
    /// The spacing, in image pixels along the curve, between reconstructed sample points.
    /// </summary>
    public const float DesiredRenderingDistance = 1f;

    private const float PiBy32 = MathF.PI / 32;

    private static ReadOnlySpan<float> ContinuousIDCTMultipliers =>
    [
        PiBy32 * 0,  PiBy32 * 1,  PiBy32 * 2,  PiBy32 * 3,  PiBy32 * 4,
        PiBy32 * 5,  PiBy32 * 6,  PiBy32 * 7,  PiBy32 * 8,  PiBy32 * 9,
        PiBy32 * 10, PiBy32 * 11, PiBy32 * 12, PiBy32 * 13, PiBy32 * 14,
        PiBy32 * 15, PiBy32 * 16, PiBy32 * 17, PiBy32 * 18, PiBy32 * 19,
        PiBy32 * 20, PiBy32 * 21, PiBy32 * 22, PiBy32 * 23, PiBy32 * 24,
        PiBy32 * 25, PiBy32 * 26, PiBy32 * 27, PiBy32 * 28, PiBy32 * 29,
        PiBy32 * 30, PiBy32 * 31,
    ];

    /// <summary>
    /// Evaluates a 32-coefficient inverse DCT at a continuous spline position.
    /// </summary>
    /// <param name="dct">The 32 coefficients borrowed from the spline.</param>
    /// <param name="t">The position within the 32-sample DCT domain.</param>
    /// <returns>The reconstructed value at that position.</returns>
    public static float ContinuousInverseDCT(ReadOnlySpan<float> dct, float t)
    {
        ref float multipliers = ref MemoryMarshal.GetReference(ContinuousIDCTMultipliers);
        ref float dctDataRef = ref MemoryMarshal.GetReference(dct);

        if (Vector<float>.Count <= 32 && Vector.IsHardwareAccelerated)
        {
            Vector<float> result = Vector<float>.Zero;
            Vector<float> tandhalf = Vector.Create(t + 0.5f);

            for (int i = 0; i < 32; i += Vector<float>.Count)
            {
                Vector<float> cosArg = Vector.LoadUnsafe(ref Unsafe.Add(ref multipliers, i)) * tandhalf;
                Vector<float> cos = Vector.Cos(cosArg);
                Vector<float> localRes = Vector.LoadUnsafe(ref Unsafe.Add(ref dctDataRef, i)) * cos;
                result = (Vector.Create(JxlDctScales.Sqrt2) * localRes) + result;
            }

            return Vector.Sum(result);
        }

        // Might have SIMD support but Vector<T> > 32 (e.g. on some CPUs),
        // so let's try different fixed-size vectors first.
        else if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> result = Vector512<float>.Zero;
            Vector512<float> tandhalf = Vector512.Create(t + 0.5f);

            for (int i = 0; i < 32; i += Vector512<float>.Count)
            {
                Vector512<float> cosArg = Vector512.LoadUnsafe(ref Unsafe.Add(ref multipliers, i)) * tandhalf;
                Vector512<float> cos = Vector512.Cos(cosArg);
                Vector512<float> localRes = Vector512.LoadUnsafe(ref Unsafe.Add(ref dctDataRef, i)) * cos;
                result = (Vector512.Create(JxlDctScales.Sqrt2) * localRes) + result;
            }

            return Vector512.Sum(result);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> result = Vector256<float>.Zero;
            Vector256<float> tandhalf = Vector256.Create(t + 0.5f);

            for (int i = 0; i < 32; i += Vector256<float>.Count)
            {
                Vector256<float> cosArg = Vector256.LoadUnsafe(ref Unsafe.Add(ref multipliers, i)) * tandhalf;
                Vector256<float> cos = Vector256.Cos(cosArg);
                Vector256<float> localRes = Vector256.LoadUnsafe(ref Unsafe.Add(ref dctDataRef, i)) * cos;
                result = (Vector256.Create(JxlDctScales.Sqrt2) * localRes) + result;
            }

            return Vector256.Sum(result);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> result = Vector128<float>.Zero;
            Vector128<float> tandhalf = Vector128.Create(t + 0.5f);

            for (int i = 0; i < 32; i += Vector128<float>.Count)
            {
                Vector128<float> cosArg = Vector128.LoadUnsafe(ref Unsafe.Add(ref multipliers, i)) * tandhalf;
                Vector128<float> cos = Vector128.Cos(cosArg);
                Vector128<float> localRes = Vector128.LoadUnsafe(ref Unsafe.Add(ref dctDataRef, i)) * cos;
                result = (Vector128.Create(JxlDctScales.Sqrt2) * localRes) + result;
            }

            return Vector128.Sum(result);
        }
        else if (Vector64.IsHardwareAccelerated)
        {
            Vector64<float> result = Vector64<float>.Zero;
            Vector64<float> tandhalf = Vector64.Create(t + 0.5f);

            for (int i = 0; i < 32; i += Vector64<float>.Count)
            {
                Vector64<float> cosArg = Vector64.LoadUnsafe(ref Unsafe.Add(ref multipliers, i)) * tandhalf;
                Vector64<float> cos = Vector64.Cos(cosArg);
                Vector64<float> localRes = Vector64.LoadUnsafe(ref Unsafe.Add(ref dctDataRef, i)) * cos;
                result = (Vector64.Create(JxlDctScales.Sqrt2) * localRes) + result;
            }

            return Vector64.Sum(result);
        }
        else
        {
            // Scalar fallback.
            float result = 0f;
            float tandhalf = t + 0.5f;

            for (int i = 0; i < 32; i++)
            {
                float cosArg = Unsafe.Add(ref multipliers, i) * tandhalf;
                float cos = MathF.Cos(cosArg);
                float localRes = Unsafe.Add(ref dctDataRef, i) * cos;
                result = (JxlDctScales.Sqrt2 * localRes) + result;
            }

            return result;
        }
    }

    // SIMD version
    private static void DrawSegmentPacked(ref JxlSplineSegment segment, bool add, int y, int x, int x0, InlineArray3<Memory<float>> rows)
    {
        Vector<float> inverseSigma = Vector.Create(segment.InverseSigma);
        Vector<float> half = Vector.Create(0.5f);
        Vector<float> oneOver2s2 = Vector.Create(0.353553391f);
        Vector<float> sigmaOver4TimesIntensity = Vector.Create(segment.SigmaOver4TimesIntensity);

        // Lane i is image column x0 + x + i. Convert the integer columns numerically before measuring distance from the center.
        Vector<float> dx = Vector.ConvertToSingle(Vector.CreateSequence(x + x0, 1)) - Vector.Create(segment.Center.X);
        Vector<float> dy = Vector.Create(y - segment.Center.Y);

        Vector<float> sqd = (dx * dx) + (dy * dy);
        Vector<float> distance = Vector.SquareRoot(sqd);

        Vector<float> oneDimensionalFactor =
            JxlSimdUtils.FastErff(((distance * half) + oneOver2s2) * inverseSigma)
            - JxlSimdUtils.FastErff(((distance * half) - oneOver2s2) * inverseSigma);

        Vector<float> localIntensity = sigmaOver4TimesIntensity * (oneDimensionalFactor * oneDimensionalFactor);

        for (int c = 0; c < 3; c++)
        {
            Span<float> currRow = rows[c].Span;
            ref float currRowRef = ref MemoryMarshal.GetReference(currRow);

            // TODO: move the add branch outside the loop and duplicate the
            // loops twice? this removes the branch
            Vector<float> cm = Vector.Create(add ? segment.Color[c] : -segment.Color[c]);

            Vector<float> @in = Vector.LoadUnsafe(ref Unsafe.Add(ref currRowRef, x));
            ((cm * localIntensity) + @in).StoreUnsafe(ref Unsafe.Add(ref currRowRef, x));
        }
    }

    // Scalar version (for remaining items left to process)
    private static void DrawSegmentScalar(ref JxlSplineSegment segment, bool add, int y, int x, int x0, InlineArray3<Memory<float>> rows)
    {
        float inverseSigma = segment.InverseSigma;
        float half = 0.5f;
        float oneOver2s2 = 0.353553391f;
        float sigmaOver4TimesIntensity = segment.SigmaOver4TimesIntensity;

        float dx = (x + x0) - segment.Center.X;
        float dy = y - segment.Center.Y;

        float sqd = (dx * dx) + (dy * dy);
        float distance = MathF.Sqrt(sqd);

        float oneDimensionalFactor =
            JxlSimdUtils.FastErff(((distance * half) + oneOver2s2) * inverseSigma)
            - JxlSimdUtils.FastErff(((distance * half) - oneOver2s2) * inverseSigma);

        float localIntensity = sigmaOver4TimesIntensity * (oneDimensionalFactor * oneDimensionalFactor);

        for (int c = 0; c < 3; c++)
        {
            Span<float> currRow = rows[c].Span;
            float cm = add ? segment.Color[c] : -segment.Color[c];
            currRow[x] = (cm * localIntensity) + currRow[x];
        }
    }

    public static void DrawSegment(ref JxlSplineSegment segment, bool add, int y, int x0, int x1, InlineArray3<Memory<float>> rows)
    {
        int start = (int)MathF.Round(segment.Center.X - segment.MaximumDistance, MidpointRounding.AwayFromZero);
        int end = (int)MathF.Round(segment.Center.X + segment.MaximumDistance, MidpointRounding.AwayFromZero);

        if (end < x0 || start >= x1)
        {
            return;  // span does not intersect scan
        }

        int spanX0 = Math.Max(x0, start) - x0;
        int spanX1 = Math.Min(x1, end + 1) - x0;

        int x = spanX0;
        if (Vector.IsHardwareAccelerated)
        {
            for (; x + Vector<float>.Count <= spanX1; x += Vector<float>.Count)
            {
                DrawSegmentPacked(ref segment, add, y, x, x0, rows);
            }
        }

        for (; x < spanX1; ++x)
        {
            DrawSegmentScalar(ref segment, add, y, x, x0, rows);
        }
    }

    /// <summary>
    /// Adds a Gaussian spline sample and the image rows that its bounded tail can affect.
    /// </summary>
    /// <param name="imageYSize">The frame height in rows.</param>
    /// <param name="center">The sample's image coordinates.</param>
    /// <param name="intensity">The arc-length weight of the sample.</param>
    /// <param name="color">The sample's three color contributions.</param>
    /// <param name="sigma">The Gaussian width.</param>
    /// <param name="segments">The caller-owned segment cache.</param>
    /// <param name="segmentSpans">The affected image-row interval for each cached segment.</param>
    public static void ComputeSegments(
        int imageYSize,
        PointF center,
        float intensity,
        InlineArray3<float> color,
        float sigma,
        List<JxlSplineSegment> segments,
        List<Range> segmentSpans)
    {
        if (!(float.IsFinite(sigma) && sigma != 0.0f && float.IsFinite(1.0f / sigma) && float.IsFinite(intensity)))
        {
            return;
        }

        // The native renderer truncates the Gaussian tail at 10^-3, adjusted by the largest channel contribution.
        const float distanceExp = 3f;

        float maxColor = MathF.Max(0.01f, MathF.Abs(color[0] * intensity));
        maxColor = MathF.Max(maxColor, MathF.Abs(color[1] * intensity));
        maxColor = MathF.Max(maxColor, MathF.Abs(color[2] * intensity));

        float maximumDistance = MathF.Sqrt(-2.0f * sigma * sigma * ((MathF.Log(0.1f) * distanceExp) - MathF.Log(maxColor)));

        int y0 = (int)MathF.Round(center.Y - maximumDistance, MidpointRounding.AwayFromZero);
        y0 = Math.Max(y0, 0);

        int y1 = (int)MathF.Round(center.Y + maximumDistance, MidpointRounding.AwayFromZero) + 1;
        y1 = Math.Min(y1, imageYSize);

        if (y1 <= y0)
        {
            return;
        }

        JxlSplineSegment segment = new()
        {
            Center = center,
            InverseSigma = 1.0f / sigma,
            SigmaOver4TimesIntensity = 0.25f * sigma * intensity,
            MaximumDistance = maximumDistance,
            Color = color
        };

        segments.Add(segment);
        segmentSpans.Add(y0..y1);
    }

    /// <summary>
    /// Applies the indexed spline segments that intersect one color row.
    /// </summary>
    /// <param name="rowX">The X color samples starting at x0.</param>
    /// <param name="rowY">The Y color samples starting at x0.</param>
    /// <param name="rowB">The B color samples starting at x0.</param>
    /// <param name="y">The image row.</param>
    /// <param name="x0">The first image column, inclusive.</param>
    /// <param name="x1">The final image column, exclusive.</param>
    /// <param name="add">Whether to add or subtract spline samples.</param>
    /// <param name="segments">The cached spline segments.</param>
    /// <param name="segmentIndices">The indices of the segments assigned to each row.</param>
    /// <param name="segmentYStart">The offsets delimiting each row's range within segmentIndices.</param>
    public static void DrawSegments(
        Memory<float> rowX,
        Memory<float> rowY,
        Memory<float> rowB,
        int y,
        int x0,
        int x1,
        bool add,
        Span<JxlSplineSegment> segments,
        ReadOnlySpan<int> segmentIndices,
        ReadOnlySpan<int> segmentYStart)
    {
        InlineArray3<Memory<float>> rows = default;
        rows[0] = rowX;
        rows[1] = rowY;
        rows[2] = rowB;

        for (int i = segmentYStart[y]; i < segmentYStart[y + 1]; i++)
        {
            DrawSegment(ref segments[segmentIndices[i]], add, y, x0, x1, rows);
        }
    }

    /// <summary>
    /// Evaluates color and width along a spline and appends its Gaussian sample segments.
    /// </summary>
    /// <param name="imageYSize">The frame height in rows.</param>
    /// <param name="spline">The reconstructed color and width coefficients.</param>
    /// <param name="pointsToDraw">The unit-spaced positions and their distance weights.</param>
    /// <param name="arcLength">The complete path length in pixels.</param>
    /// <param name="segments">The caller-owned segment cache.</param>
    /// <param name="segmentsSpans">The image-row interval for each segment.</param>
    public static void SegmentsFromPoints(
        int imageYSize,
        JxlSpline spline,
        List<(PointF Point, float Multiplier)> pointsToDraw,
        float arcLength,
        List<JxlSplineSegment> segments,
        List<Range> segmentsSpans)
    {
        float inverseArcLength = 1.0f / arcLength;
        int k = 0;

        foreach ((PointF point, float multiplier) in pointsToDraw)
        {
            float progressAlongArc = MathF.Min(1.0f, (k++ * DesiredRenderingDistance) * inverseArcLength);

            InlineArray3<float> color = default;
            color[0] = ContinuousInverseDCT(spline.ColorDct[0], (32 - 1) * progressAlongArc);
            color[1] = ContinuousInverseDCT(spline.ColorDct[1], (32 - 1) * progressAlongArc);
            color[2] = ContinuousInverseDCT(spline.ColorDct[2], (32 - 1) * progressAlongArc);

            float sigma = ContinuousInverseDCT(spline.SigmaDct, (32 - 1) * progressAlongArc);
            ComputeSegments(imageYSize, point, multiplier, color, sigma, segments, segmentsSpans);
        }
    }

    /// <summary>
    /// Appends the centripetal Catmull-Rom curve through the supplied control points.
    /// </summary>
    /// <param name="points">The validated control points, with no equal adjacent points.</param>
    /// <param name="result">The caller-owned list that receives 16 samples per interval and the final endpoint.</param>
    public static void DrawCentripetalCatmullRomSpline(ReadOnlySpan<PointF> points, List<PointF> result)
    {
        if (points.Length == 1)
        {
            result.Add(points[0]);
            return;
        }

        const int samplesPerInterval = 16;

        // Each interval needs four points. Extrapolate the two endpoint neighbors in a fixed local block,
        // rather than copying and extending the complete control-point list.
        for (int start = 0; start < points.Length - 1; start++)
        {
            InlineArray4<PointF> pointStorage = default;
            Span<PointF> p = pointStorage;
            p[0] = start == 0 ? points[0] + (points[0] - points[1]) : points[start - 1];
            p[1] = points[start];
            p[2] = points[start + 1];
            p[3] = start + 2 == points.Length ? points[^1] + (points[^1] - points[^2]) : points[start + 2];
            result.Add(p[1]);

            InlineArray3<float> distanceStorage = default;
            InlineArray4<float> parameterStorage = default;
            Span<float> distances = distanceStorage;
            Span<float> parameters = parameterStorage;

            // Centripetal parameter steps are the square roots of Euclidean distances.
            // Adjacent control points differ, so every interpolation denominator is positive.
            for (int k = 0; k < 3; k++)
            {
                distances[k] = MathF.Sqrt(float.Hypot(p[k + 1].X - p[k].X, p[k + 1].Y - p[k].Y));
                parameters[k + 1] = parameters[k] + distances[k];
            }

            for (int i = 1; i < samplesPerInterval; i++)
            {
                float position = distances[0] + (((float)i / samplesPerInterval) * distances[1]);
                InlineArray3<PointF> a = default;

                for (int k = 0; k < 3; k++)
                {
                    a[k] = p[k] + (((position - parameters[k]) / distances[k]) * (p[k + 1] - p[k]));
                }

                InlineArray2<PointF> b = default;

                for (int k = 0; k < 2; k++)
                {
                    b[k] = a[k] + (((position - parameters[k]) / (distances[k] + distances[k + 1])) * (a[k + 1] - a[k]));
                }

                result.Add(b[0] + (((position - parameters[1]) / distances[1]) * (b[1] - b[0])));
            }
        }

        result.Add(points[^1]);
    }

    /// <summary>
    /// Appends points at unit arc-length steps and the final residual endpoint.
    /// </summary>
    /// <param name="points">The nonempty interpolated path.</param>
    /// <param name="result">The caller-owned list of positions and their distance weights.</param>
    public static void GetEquallySpacedPoints(ReadOnlySpan<PointF> points, List<(PointF Point, float Multiplier)> result)
    {
        PointF current = points[0];
        result.Add((current, DesiredRenderingDistance));
        int next = 0;

        while (next < points.Length)
        {
            PointF previous = current;
            float distanceFromPrevious = 0F;

            while (true)
            {
                if (next == points.Length)
                {
                    // Keep the final weight even when it is zero. The caller uses it to recover the exact path length.
                    result.Add((previous, distanceFromPrevious));
                    return;
                }

                PointF delta = points[next] - previous;
                float distanceToNext = MathF.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));

                if (distanceFromPrevious + distanceToNext >= DesiredRenderingDistance)
                {
                    current = previous + (((DesiredRenderingDistance - distanceFromPrevious) / distanceToNext) * delta);
                    result.Add((current, DesiredRenderingDistance));
                    break;
                }

                distanceFromPrevious += distanceToNext;
                previous = points[next++];
            }
        }
    }
}
