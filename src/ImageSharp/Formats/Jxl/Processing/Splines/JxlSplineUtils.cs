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

    public static float ContinuousInverseDCT(in Dct32 dct, float t)
    {
        ref float multipliers = ref MemoryMarshal.GetReference(ContinuousIDCTMultipliers);
        ReadOnlySpan<float> dctData = dct;
        ref float dctDataRef = ref MemoryMarshal.GetReference(dctData);

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

        Vector<float> dx = JxlSimdUtils.Iota(x + x0).As<int, float>() - Vector.Create(segment.Center.X);
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
        for (; x + Vector<float>.Count <= spanX1; x += Vector<float>.Count)
        {
            DrawSegmentPacked(ref segment, add, y, x, x0, rows);
        }

        for (; x < spanX1; ++x)
        {
            DrawSegmentScalar(ref segment, add, y, x, x0, rows);
        }
    }

    public static void ComputeSegments(int imageYSize, PointF center, float intensity, InlineArray3<float> color, float sigma, List<JxlSplineSegment> segments, List<JxlSplineSegmentSpan> segmentSpans)
    {
        if (!(float.IsFinite(sigma) && sigma != 0.0f && float.IsFinite(1.0f / sigma) && float.IsFinite(intensity)))
        {
            return;
        }

        // This is about 30% faster, but for higher precision
        // one can change this to 5 instead.
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
        segmentSpans.Add(new JxlSplineSegmentSpan(y0, y1));
    }

    public static void DrawSegments(Memory<float> rowX, Memory<float> rowY, Memory<float> rowB, int y, int x0, int x1, bool add, Span<JxlSplineSegment> segments, Span<int> segmentIndices, Span<int> segmentYStart)
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

    public static void SegmentsFromPoints(int imageYSize, JxlSpline spline, List<(PointF Point, float Multiplier)> pointsToDraw, float arcLength, List<JxlSplineSegment> segments, List<JxlSplineSegmentSpan> segmentsSpans)
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

    public static void DrawCentripetalCatmullRomSpline(Span<PointF> points, List<PointF> result)
    {
        if (points.Length == 0)
        {
            return;
        }

        if (points.Length == 1)
        {
            result.Add(points[0]);
            return;
        }

        List<PointF> pointsCopy = [];
        for (int i = 0; i < points.Length; i++)
        {
            pointsCopy.Add(points[i]);
        }

        const int numPoints = 16;
        pointsCopy.Insert(0, pointsCopy[0] + (pointsCopy[0] - pointsCopy[1]));
        pointsCopy.Add(pointsCopy[^1] + (pointsCopy[^1] - pointsCopy[^2]));

        for (int start = 0; start < pointsCopy.Count - 3; start++)
        {
            Span<PointF> p = CollectionsMarshal.AsSpan(pointsCopy)[start..];
            result.Add(p[1]);

            InlineArray3<float> d = default;
            InlineArray4<float> t = default;

            for (int k = 0; k < 3; ++k)
            {
                d[k] = MathF.Sqrt(JxlMath.Hypot(p[k + 1].X - p[k].X, p[k + 1].Y - p[k].Y));
                t[k + 1] = t[k] + d[k];
            }

            for (int i = 1; i < numPoints; ++i)
            {
                float tt = d[0] + (((float)i / numPoints) * d[1]);
                InlineArray3<PointF> a = default;

                for (int k = 0; k < 3; ++k)
                {
                    a[k] = p[k] + (((tt - t[k]) / d[k]) * (p[k + 1] - p[k]));
                }

                InlineArray3<PointF> b = default;

                for (int k = 0; k < 2; ++k)
                {
                    b[k] = a[k] + (((tt - t[k]) / (d[k] + d[k + 1])) * (a[k + 1] - a[k]));
                }

                result.Add(b[0] + (((tt - t[1]) / d[1]) * (b[1] - b[0])));
            }
        }

        result.Add(pointsCopy[^2]);
    }

    public static void ForEachEquallySpacedPoint(Span<PointF> points, Action<PointF, float> functor)
    {
        PointF current = points[0];
        functor(current, DesiredRenderingDistance);

        ref PointF next = ref points[0];
        ref PointF end = ref points[^1]; // last

        while (!Unsafe.AreSame(ref next, ref end))
        {
            ref PointF previous = ref current;
            float arcLengthFromPrevious = 0f;

            while (true)
            {
                if (next == end)
                {
                    functor(previous, arcLengthFromPrevious);
                    return;
                }

                float arcLengthToNext = MathF.Sqrt(SquaredNorm(next - previous));

                if (arcLengthFromPrevious + arcLengthToNext >= DesiredRenderingDistance)
                {
                    current = previous + (((DesiredRenderingDistance - arcLengthFromPrevious) / arcLengthToNext) * (next - previous));
                    functor(current, DesiredRenderingDistance);
                    break;
                }

                arcLengthFromPrevious += arcLengthToNext;
                previous = ref next;
                next = ref Unsafe.Add(ref next, 1);
            }
        }
    }

    private static float SquaredNorm(PointF pointF)
    {
        float x = pointF.X;
        float y = pointF.Y;

        return (x * x) + (y * y);
    }
}
