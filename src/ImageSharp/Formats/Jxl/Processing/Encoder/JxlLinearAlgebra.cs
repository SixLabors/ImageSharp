// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Matrix2x2 = System.Runtime.CompilerServices.InlineArray2<System.Runtime.CompilerServices.InlineArray2<double>>;
using Vector2 = System.Runtime.CompilerServices.InlineArray2<double>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Handles linear algebra for encoding.
/// </summary>
internal static class JxlLinearAlgebra
{
    public static void ConvertToDiagonal(Matrix2x2 a, ref Vector2 diag, ref Matrix2x2 u)
    {
        DebugGuard.MustBeLessThan(Math.Abs(a[0][1] - a[1][0]), 1e-15, nameof(a));

        double b = -(a[0][0] + a[1][1]);
        double c = (a[0][0] * a[1][1]) - (a[0][1] * a[0][1]);
        double d = (b * b) - (4.0 * c);

        if (Math.Abs(a[0][1]) < 1e-10 || d < 0)
        {
            // Already diagonal.
            diag[0] = a[0][0];
            diag[1] = a[1][1];
            u[0][0] = u[1][1] = 1.0;
            u[0][1] = u[1][0] = 0.0;
            return;
        }

        double sqd = Math.Sqrt(d);
        double l1 = (-b - sqd) * 0.5;
        double l2 = (-b + sqd) * 0.5;

        Vector2 v1 = default;
        v1[0] = a[0][0] - l1;
        v1[1] = a[1][0];

        double v1n = 1.0 / JxlMath.Hypot(v1[0], v1[1]);
        v1[0] = v1[0] * v1n;
        v1[1] = v1[1] * v1n;

        diag[0] = l1;
        diag[1] = l2;

        u[0][0] = v1[1];
        u[0][1] = -v1[0];
        u[1][0] = v1[0];
        u[1][1] = v1[1];
    }
}
