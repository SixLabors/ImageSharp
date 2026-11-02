// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using Matrix2x2 = System.Runtime.CompilerServices.InlineArray2<System.Runtime.CompilerServices.InlineArray2<double>>;
using Vector2 = System.Runtime.CompilerServices.InlineArray2<double>;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Encoder;

public class LinearAlgebraTests
{
    private static Vector2 CreateVector2(double a, double b)
    {
        Vector2 v2 = default;

        v2[0] = a;
        v2[1] = b;

        return v2;
    }

    private static Matrix2x2 Diagonal(Vector2 d)
    {
        Matrix2x2 result = default;

        result[0][0] = d[0];
        result[0][1] = 0;
        result[1][0] = 0;
        result[1][1] = d[1];

        return result;
    }

    private static Matrix2x2 Identity() => Diagonal(CreateVector2(1.0f, 1.0f));

    private static Matrix2x2 MatrixMultiply(Matrix2x2 a, Matrix2x2 b)
    {
        Matrix2x2 result = default;

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                result[y][x] = (a[0][x] * b[y][0]) + (a[1][x] * b[y][1]);
            }
        }

        return result;
    }

    private static Matrix2x2 Transpose(Matrix2x2 a)
    {
        Matrix2x2 result = default;

        result[0] = CreateVector2(a[0][0], a[1][0]);
        result[1] = CreateVector2(a[0][1], a[1][1]);

        return result;
    }

    private static Matrix2x2 RandomSymmetricMatrix(
        Rng rng,
        float vmin,
        float vmax)
    {
        Matrix2x2 result = default;

        result[0][0] = rng.UniformF(vmin, vmax);
        result[0][1] = rng.UniformF(vmin, vmax);
        result[1][0] = result[0][1];
        result[1][1] = rng.UniformF(vmin, vmax);

        return result;
    }

    private static void VerifyMatrixEqual(Matrix2x2 a, Matrix2x2 b, float epsilon)
    {
        TolerantMath comparer = new(epsilon);

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                Assert.True(
                    comparer.AreEqual(a[y][x], b[y][x]),
                    $"Matrices differ at [{y}][{x}]: {a[y][x]} != {b[y][x]}");
            }
        }
    }

    private static void VerifyOrthogonal(Matrix2x2 a, float epsilon) => VerifyMatrixEqual(
        Identity(),
        MatrixMultiply(Transpose(a), a),
        epsilon);

    [Fact]
    public void ConvertToDiagonal()
    {
        {
            Matrix2x2 i = Identity();
            Matrix2x2 u = default;
            Vector2 d = default;

            JxlLinearAlgebra.ConvertToDiagonal(i, ref d, ref u);

            VerifyMatrixEqual(i, u, 1e-15f);

            for (int k = 0; k < 2; k++)
            {
                Assert.True(
                    Math.Abs(d[k] - 1.0f) <= 1e-15f,
                    $"d[{k}] = {d[k]}");
            }
        }

        {
            Matrix2x2 a = Identity();
            a[0][1] = 2.0f;
            a[1][0] = 2.0f;

            Matrix2x2 u = default;
            Vector2 d = default;

            JxlLinearAlgebra.ConvertToDiagonal(a, ref d, ref u);

            VerifyOrthogonal(u, 1e-12f);
            VerifyMatrixEqual(
                a,
                MatrixMultiply(u, MatrixMultiply(Diagonal(d), Transpose(u))),
                1e-12f);
        }

        {
            Matrix2x2 a = default;
            a[0] = CreateVector2(0.000208649f, 1.13687e-12f);
            a[1] = CreateVector2(1.13687e-12f, 0.000208649f);

            Matrix2x2 u = default;
            Vector2 d = default;

            JxlLinearAlgebra.ConvertToDiagonal(a, ref d, ref u);

            VerifyOrthogonal(u, 1e-12f);
            VerifyMatrixEqual(
                a,
                MatrixMultiply(u, MatrixMultiply(Diagonal(d), Transpose(u))),
                1e-11f);
        }

        {
            Rng rng = new(0);

            for (int i = 0; i < 1_000_000; i++)
            {
                Matrix2x2 a = RandomSymmetricMatrix(rng, -1.0f, 1.0f);
                Matrix2x2 u = default;
                Vector2 d = default;

                JxlLinearAlgebra.ConvertToDiagonal(a, ref d, ref u);

                VerifyOrthogonal(u, 1e-12f);
                VerifyMatrixEqual(
                    a,
                    MatrixMultiply(u, MatrixMultiply(Diagonal(d), Transpose(u))),
                    5e-10f);
            }
        }
    }
}
