// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using Matrix2x2 = System.Runtime.CompilerServices.InlineArray2<System.Runtime.CompilerServices.InlineArray2<double>>;
using Vector2 = System.Runtime.CompilerServices.InlineArray2<double>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.DotDetection;

internal static class JxlDotDetectionUtils
{
    public const int EllipseWindowSize = 5;

    /// <summary>
    /// Maximum area in pixels of an ellipse.
    /// </summary>
    public const int MaxCCSize = 1000;

    public const bool OptimizeBackground = true;

    /// <summary>
    /// Sum of Square Differences
    /// </summary>
    /// <param name="configuration">Contains the memory allocator and parallel options.</param>
    /// <param name="original">Original image</param>
    /// <param name="smooth">Smooth image</param>
    /// <returns>Sum of Square Differences coefficients</returns>
    public static JxlImageF Ssd(Configuration configuration, JxlImage3F original, JxlImage3F smooth)
    {
        Vector<float> colorCoef0 = Vector<float>.Zero;
        Vector<float> colorCoef1 = Vector.Create(10.0f);
        Vector<float> colorCoef2 = Vector<float>.Zero;

        JxlImageF sumOfSquares = new(configuration, original.XSize, original.YSize);

        _ = Parallel.For(0, original.YSize, configuration.GetParallelOptions(), y =>
        {
            Span<float> originalRow0 = original.Plane(0).GetRow(y);
            Span<float> originalRow1 = original.Plane(1).GetRow(y);
            Span<float> originalRow2 = original.Plane(2).GetRow(y);

            Span<float> smoothRow0 = smooth.Plane(0).GetRow(y);
            Span<float> smoothRow1 = smooth.Plane(1).GetRow(y);
            Span<float> smoothRow2 = smooth.Plane(2).GetRow(y);

            Span<float> sumOfSquaresRow = sumOfSquares.GetRow(y);

            for (int x = 0; x < original.XSize; x += Vector<float>.Count)
            {
                Vector<float> v0 = Vector.Create<float>(originalRow0[x..]) - Vector.Create<float>(smoothRow0[x..]);
                Vector<float> v1 = Vector.Create<float>(originalRow1[x..]) - Vector.Create<float>(smoothRow1[x..]);
                Vector<float> v2 = Vector.Create<float>(originalRow2[x..]) - Vector.Create<float>(smoothRow2[x..]);

                v0 = (v0 * v0) * colorCoef0;
                v1 = (v1 * v1) * colorCoef1;
                v2 = (v2 * v2) * colorCoef2;

                Vector<float> weightedSumOfSquareDiffs = v0 + v1 + v2;
                weightedSumOfSquareDiffs.CopyTo(sumOfSquaresRow[x..]);
            }
        });

        return sumOfSquares;
    }

    /// <summary>
    /// Creates Gaussian that smooths noise but preserves dots.
    /// </summary>
    /// <returns>Gaussian</returns>
    public static JxlWeightsSeparable5 WeightsSeparable5Gaussian0_65()
    {
        const float w0 = 0.558311f;
        const float w1 = 0.210395f;
        const float w2 = 0.010449f;

        JxlWeightsSeparable5 weights = default;

        // HWY_REP4(w0), HWY_REP4(w1), HWY_REP4(w2)
        weights.Horizontal[0] = w0;
        weights.Horizontal[1] = w0;
        weights.Horizontal[2] = w0;
        weights.Horizontal[3] = w0;

        weights.Horizontal[4] = w1;
        weights.Horizontal[5] = w1;
        weights.Horizontal[6] = w1;
        weights.Horizontal[7] = w1;

        weights.Horizontal[8] = w2;
        weights.Horizontal[9] = w2;
        weights.Horizontal[10] = w2;
        weights.Horizontal[11] = w2;

        // Same weights for vertical
        weights.Vertical = weights.Horizontal;

        return weights;
    }

    /// <summary>
    /// Creates Gaussian that removes dots.
    /// </summary>
    /// <returns>Gaussian</returns>
    public static JxlWeightsSeparable5 WeightsSeparable5Gaussian3()
    {
        const float w0 = 0.222338f;
        const float w1 = 0.210431f;
        const float w2 = 0.1784f;

        JxlWeightsSeparable5 weights = default;

        // HWY_REP4(w0), HWY_REP4(w1), HWY_REP4(w2)
        weights.Horizontal[0] = w0;
        weights.Horizontal[1] = w0;
        weights.Horizontal[2] = w0;
        weights.Horizontal[3] = w0;

        weights.Horizontal[4] = w1;
        weights.Horizontal[5] = w1;
        weights.Horizontal[6] = w1;
        weights.Horizontal[7] = w1;

        weights.Horizontal[8] = w2;
        weights.Horizontal[9] = w2;
        weights.Horizontal[10] = w2;
        weights.Horizontal[11] = w2;

        // Same weights for vertical
        weights.Vertical = weights.Horizontal;

        return weights;
    }

    public static double DotGaussianModel(double dx, double dy, double ct, double st, double sigmaX, double sigmaY, double intensity)
    {
        double rx = (ct * dx) + (st * dy);
        double ry = (-st * dx) + (ct * dy);
        double md = (rx * rx / sigmaX) + (ry * ry / sigmaY);
        double value = intensity * Math.Exp(-0.5 * md);

        return value;
    }

    public static bool PointInRect(in Rectangle r, in Point p)
        => (uint)p.X >= r.X0() &&
            (uint)p.X < (r.X0() + r.Width)
            && (uint)p.Y >= r.Y0()
            && (uint)p.Y < (r.Y0() + r.Height);

    public static Rectangle BoundingRectangle(Span<Point> pixels)
    {
        DebugGuard.MustBeGreaterThan(pixels.Length, 0, nameof(pixels));

        int lowX = pixels[0].X;
        int highX = pixels[0].X;
        int lowY = pixels[0].Y;
        int highY = pixels[0].Y;

        foreach (Point p in pixels)
        {
            lowX = Math.Min(lowX, p.X);
            highX = Math.Max(highX, p.X);
            lowY = Math.Min(lowY, p.Y);
            highY = Math.Max(highY, p.Y);
        }

        return new Rectangle(
            lowX,
            lowY,
            highX - lowX + 1,
            highY - lowY + 1);
    }

    public static JxlGaussianEllipse? FitGaussianFast(JxlConnectedComponent cc, Rectangle rect, JxlImage3F img, JxlImage3F background)
    {
        const bool leastSqIntensity = true;
        const double epsilon = 1e-6;
        const int rectBounds = EllipseWindowSize >> 1;

        JxlGaussianEllipse ans = default;
        double sum = 0;
        int n = 0;

        // All values are initialized to 0 by default.
        InlineArray3<double> m1 = default;
        InlineArray3<double> m2 = default;
        InlineArray3<double> color = default;
        InlineArray3<double> bgColor = default;

        for (int c = 0; c < 3; c++)
        {
            color[c] = img.PlaneRow(rect, c, cc.Mode.Y)[cc.Mode.X] -
                       background.PlaneRow(rect, c, cc.Mode.Y)[cc.Mode.X];
        }

        double sign = (color[1] > 0) ? 1 : -1;

        for (int sy = -rectBounds; sy <= rectBounds; sy++)
        {
            int y = sy + cc.Mode.Y;

            // Bounds check
            if (y < 0 || y >= rect.Height)
            {
                continue;
            }

            Span<float> row = img.PlaneRow(rect, 1, y);
            Span<float> bgrow = background.PlaneRow(rect, 1, y);

            for (int sx = -rectBounds; sx <= rectBounds; sx++)
            {
                int x = sx + cc.Mode.X;

                // Bounds check
                if (x < 0 || x >= rect.Width)
                {
                    continue;
                }

                double w = Math.Max(epsilon, sign * (row[x] - bgrow[x]));
                sum += w;

                m1[0] += w * x;
                m1[1] += w * y;
                m2[0] += w * x * x;
                m2[1] += w * x * y;
                m2[2] += w * y * y;

                for (int c = 0; c < 3; c++)
                {
                    bgColor[c] += background.PlaneRow(rect, c, y)[x];
                }

                n++;
            }
        }

        if (n <= 0)
        {
            // n must be at least 1
            return null;
        }

        for (int i = 0; i < 3; i++)
        {
            m1[i] /= sum;
            m2[i] /= sum;
            bgColor[i] /= n;
        }

        double sigmaMult = 1.0;

        // This inline array is read-only. It contains scale multipliers.
        InlineArray3<double> scaleMult = default;
        scaleMult[0] = 1.1;
        scaleMult[1] = 1.1;
        scaleMult[2] = 1.1;

        ans.X = m1[0];
        ans.Y = m1[1];

        ans.Intensity[0] = scaleMult[0] * color[0];
        ans.Intensity[1] = scaleMult[1] * color[1];
        ans.Intensity[2] = scaleMult[2] * color[2];

        Matrix2x2 sigma = default;
        Vector2 d = default;
        Matrix2x2 uMatrix = default;

        sigma[0][0] = m2[0] - (m1[0] * m1[0]);
        sigma[1][1] = m2[2] - (m1[1] * m1[1]);
        sigma[0][1] = sigma[1][0] = m2[1] - (m1[0] * m1[1]);

        JxlLinearAlgebra.ConvertToDiagonal(sigma, ref d, ref uMatrix);

        Vector2 u = default;
        u[0] = uMatrix[1][0];
        u[1] = uMatrix[1][1];

        int p1 = 0;
        int p2 = 1;

        if (d[0] < d[1])
        {
            RuntimeUtility.Swap(ref p1, ref p2);
        }

        ans.SigmaX = sigmaMult * d[p1];
        ans.SigmaY = sigmaMult * d[p2];
        ans.Angle = Math.Atan2(u[p1], u[p2]);
        ans.L2Loss = 0;
        ans.BackgroundColor = bgColor;

        if (leastSqIntensity)
        {
            ref JxlGaussianEllipse ellipse = ref ans;

            (double ct, double st) = Math.SinCos(ans.Angle);

            // Estimate intensity with least squares (fixed background)
            for (int c = 0; c < 3; c++)
            {
                double gg = 0.0;
                double gd = 0.0;

                int yc = cc.Mode.Y;
                int xc = cc.Mode.X;

                for (int y = yc - rectBounds; y <= yc + rectBounds; y++)
                {
                    if (y < 0 || y >= rect.Height)
                    {
                        continue;
                    }

                    Span<float> row = img.PlaneRow(rect, c, y);
                    Span<float> bgrow = background.PlaneRow(rect, c, y);

                    for (int x = xc - rectBounds; x <= xc + rectBounds; x++)
                    {
                        if (x < 0 || x >= rect.Width)
                        {
                            continue;
                        }

                        double target = row[x] - bgrow[x];
                        double gaussian = DotGaussianModel(x - ellipse.X, y - ellipse.Y, ct, st, ellipse.SigmaX, ellipse.SigmaY, 1.0);

                        gg += gaussian * gaussian;
                        gd += gaussian * target;
                    }
                }

                ans.Intensity[c] = gd / (gg + 1e-6);
            }
        }

        ComputeDotLossless(ref ans, cc, rect, img, background);
        return ans;
    }

    public static void ComputeDotLossless(ref JxlGaussianEllipse ellipse, JxlConnectedComponent cc, Rectangle rect, JxlImage3F img, JxlImage3F background)
    {
        const int rectBounds = 2;
        const double intensityR = 1.0;
        const double sigmaR = 0.0;
        const double zeroEpsilon = 0.1;

        (double ct, double st) = Math.SinCos(ellipse.Angle);

        // Read-only
        InlineArray3<double> channelGains = default;
        channelGains[0] = 1.0;
        channelGains[1] = 1.0;
        channelGains[2] = 1.0;

        int n = 0;

        ellipse.L1Loss = 0;
        ellipse.L2Loss = 0;
        ellipse.NegativePixels = 0;
        ellipse.NegativeValue[0] = 0;
        ellipse.NegativeValue[1] = 0;
        ellipse.NegativeValue[2] = 0;
        ellipse.CustomLoss = 0;

        double distMeanModeSq = ((cc.Mode.X - ellipse.X) * (cc.Mode.X - ellipse.X)) + ((cc.Mode.Y - ellipse.Y) * (cc.Mode.Y - ellipse.Y));

        for (int c = 0; c < 3; c++)
        {
            for (int sy = -rectBounds; sy < cc.Bounds.Height + rectBounds; sy++)
            {
                int y = sy + cc.Bounds.Y0();

                if (y < 0 || y >= rect.Height)
                {
                    continue;
                }

                Span<float> row = img.PlaneRow(rect, c, y);
                Span<float> bgrow = background.PlaneRow(rect, c, y);

                for (int sx = -rectBounds; sx < cc.Bounds.Width + rectBounds; sx++)
                {
                    int x = sx + cc.Bounds.X0();

                    if (x < 0 || x >= rect.Width)
                    {
                        continue;
                    }

                    double target = row[x];
                    double dotDelta = DotGaussianModel(x - ellipse.X, y - ellipse.Y, ct, st, ellipse.SigmaX, ellipse.SigmaY, ellipse.Intensity[c]);

                    if (dotDelta > target + zeroEpsilon)
                    {
                        ellipse.NegativePixels++;
                        ellipse.NegativeValue[c] += dotDelta - target;
                    }

                    double bkg = OptimizeBackground ? ellipse.BackgroundColor[c] : bgrow[x];
                    double pred = bkg + dotDelta;
                    double diff = target - pred;
                    double l2 = channelGains[c] * diff * diff;
                    double l1 = channelGains[c] * Math.Abs(diff);

                    ellipse.L2Loss += l2;
                    ellipse.L1Loss += l1;

                    double w = DotGaussianModel(x - cc.Mode.X, y - cc.Mode.Y, 1.0, 0.0, 1.0 + ellipse.SigmaX, 1.0 + ellipse.SigmaY, 1.0);
                    ellipse.CustomLoss += w * l2;
                    n++;
                }
            }
        }

        ellipse.L2Loss /= n;
        ellipse.CustomLoss /= n;
        ellipse.CustomLoss += (20.0 * distMeanModeSq) + ellipse.NegativeValue[1];
        ellipse.L1Loss /= n;

        double ridgeTerm = (sigmaR * ellipse.SigmaX) + (sigmaR * ellipse.SigmaY);

        for (int c = 0; c < 3; c++)
        {
            ridgeTerm += intensityR * ellipse.Intensity[c] * ellipse.Intensity[c];
        }

        ellipse.RidgeLoss = ellipse.L2Loss + ridgeTerm;
    }

    public static JxlGaussianEllipse? FitGaussian(JxlConnectedComponent cc, Rectangle rect, JxlImage3F img, JxlImage3F background)
    {
        JxlGaussianEllipse? ellipseOrNull = FitGaussianFast(cc, rect, img, background);

        if (ellipseOrNull is null)
        {
            return null;
        }

        JxlGaussianEllipse ellipse = ellipseOrNull.Value;

        if (ellipse.SigmaX < ellipse.SigmaY)
        {
            RuntimeUtility.Swap(ref ellipse.SigmaX, ref ellipse.SigmaY);
            ellipse.Angle += MathF.PI / 2.0;
        }

        ellipse.Angle -= Math.PI * Math.Floor(ellipse.Angle / Math.PI);

        if (Math.Abs(ellipse.Angle - Math.PI) < 1e-6 || Math.Abs(ellipse.Angle) < 1e-6)
        {
            ellipse.Angle = 0.0;
        }

        if (!(ellipse.Angle >= 0 && ellipse.Angle <= Math.PI && ellipse.SigmaX >= ellipse.SigmaY))
        {
            return null;
        }

        return ellipse;
    }

    public static bool ExtractComponent(Rectangle rect, JxlImageF img, List<Point> pixels, Point seed, double threshold)
    {
        Span<Point> neighbors =
        [
            new(1, -1),
            new(1, 0),
            new(1, 1),
            new(0, -1),
            new(0, 1),
            new(-1, -1),
            new(-1, 1),
            new(1, 0)
        ];

        List<Point> q = [seed];

        while (q.Count > 0)
        {
            Point current = q[^1];
            q.RemoveAt(q.Count - 1);

            pixels.Add(current);

            if (pixels.Count > MaxCCSize)
            {
                return false;
            }

            foreach (Point delta in neighbors)
            {
                Point child = new(current.X + delta.X, current.Y + delta.Y);

                if (child.X >= 0 && child.X < rect.Width &&
                    child.Y >= 0 && child.Y < rect.Height)
                {
                    ref float value = ref img.GetRow(rect, child.Y)[child.X];

                    if (value > threshold)
                    {
                        value = 0.0f;
                        q.Add(child);
                    }
                }
            }
        }

        return true;
    }

    public static JxlImageF? ComputeEnergyImage(Configuration configuration, JxlImage3F original, ref JxlImage3F smooth)
    {
        using JxlImage3F forig = new(configuration, original.XSize, original.YSize);
        smooth = new(configuration, original.XSize, original.YSize);

        Rectangle rect = original.GetRectangle();

        JxlWeightsSeparable5 weights1 = WeightsSeparable5Gaussian0_65();
        JxlWeightsSeparable5 weights3 = WeightsSeparable5Gaussian3();

        for (int c = 0; c < 3; c++)
        {
            if (!Separable5(configuration, original.Plane(c), rect, weights3, forig.Plane(c)))
            {
                return null;
            }

            if (!Separable5(configuration, forig.Plane(c), rect, weights3, smooth.Plane(c)))
            {
                return null;
            }

            if (!Separable5(configuration, original.Plane(c), rect, weights1, forig.Plane(c)))
            {
                return null;
            }
        }

        return Ssd(configuration, forig, smooth);
    }

    public static List<JxlConnectedComponent> FindCC(Configuration configuration, JxlImageF energy, Rectangle rect, double tLow, double tHigh, int maxWindow, double minScore)
    {
        const int extraRect = 4;

        using JxlImageF img = new(configuration, energy.XSize, energy.YSize);

        if (!JxlImageOperations.CopyImage(energy, img))
        {
            throw new InvalidOperationException("Couldn't copy image");
        }

        List<JxlConnectedComponent> ans = [];

        for (int y = 0; y < rect.Height; y++)
        {
            Span<float> row = img.GetRow(rect, y);

            for (int x = 0; x < rect.Width; x++)
            {
                if (row[x] > tHigh)
                {
                    List<Point> pixels = [];
                    row[x] = 0.0f;

                    Point seed = new(x, y);
                    bool success = ExtractComponent(rect, img, pixels, seed, tLow);

                    if (!success)
                    {
                        continue;
                    }

                    Rectangle bounds = BoundingRectangle(CollectionsMarshal.AsSpan(pixels));

                    if (bounds.Width < maxWindow && bounds.Height < maxWindow)
                    {
                        JxlConnectedComponent cc = new(bounds, pixels);

                        cc.ComputeStats(energy, rect, extraRect);

                        if (cc.Score < minScore)
                        {
                            continue;
                        }

                        ans.Add(cc);
                    }
                }
            }
        }

        return ans;
    }

    public static List<JxlPatchInfo> DetectGaussianEllipses(
        Configuration configuration,
        JxlImage3F opsin,
        Rectangle rect,
        JxlGaussianDetectParameters parameters,
        JxlEllipseQuantParameters qparameters)
    {
        List<JxlPatchInfo> dots = [];

        JxlImage3F smooth = new(configuration, opsin.XSize, opsin.YSize);
        JxlImageF energy = ComputeEnergyImage(configuration, opsin, ref smooth) ?? throw new InvalidOperationException("Failed to compute energy image");
        List<JxlConnectedComponent> components = FindCC(configuration, energy, rect, parameters.TLow, parameters.THigh, parameters.MaxWindowSize, parameters.MinScore);
        int numCC = Math.Min(parameters.MaxCC, (components.Count * parameters.PercCC) / 100);

        if (components.Count > numCC)
        {
            components.Sort((a, b) => b.Score.CompareTo(a.Score));
            components.RemoveRange(numCC, components.Count - numCC);
        }

        foreach (JxlConnectedComponent cc in components)
        {
            JxlGaussianEllipse ellipse = FitGaussian(cc, rect, opsin, smooth) ?? throw new InvalidOperationException("Failed to fit Gaussian");

            if (ellipse.X < 0.0 || Math.Ceiling(ellipse.X) >= rect.Width ||
                ellipse.Y < 0.0 || Math.Ceiling(ellipse.Y) >= rect.Height)
            {
                continue;
            }

            if (ellipse.NegativePixels > parameters.MaxNegativePixels)
            {
                continue;
            }

            double intensity = (0.21 * ellipse.Intensity[0]) + (0.72 * ellipse.Intensity[1]) + (0.07 * ellipse.Intensity[2]);
            double intensitySq = intensity * intensity;

            double sqDistMeanMode = ((ellipse.X - cc.Mode.X) * (ellipse.X - cc.Mode.X)) +
                                    ((ellipse.Y - cc.Mode.Y) * (ellipse.Y - cc.Mode.Y));

            if (ellipse.L2Loss < parameters.MaxL2Loss &&
                ellipse.CustomLoss < parameters.MaxCustomLoss &&
                intensitySq > (parameters.MinIntensity * parameters.MinIntensity) &&
                sqDistMeanMode < parameters.MaxDistMeanMode * parameters.MaxDistMeanMode)
            {
                int x0 = cc.Bounds.X0();
                int y0 = cc.Bounds.Y0();

                dots.Add(new());
                dots[^1].Second.Add(new Point(x0, y0));

                JxlQuantizedPatch patch = dots[^1].First;
                patch.Width = cc.Bounds.Width;
                patch.Height = cc.Bounds.Height;

                for (int y = 0; y < patch.ysize; y++)
                {
                    for (int x = 0; x < patch.xsize; x++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            patch.fpixels[c][(y * patch.xsize) + x] =
                                opsin.PlaneRow(rect, c, y0 + y)[x0 + x] -
                                smooth.PlaneRow(rect, c, y0 + y)[x0 + x];
                        }
                    }
                }
            }
        }

        return dots;
    }
}
