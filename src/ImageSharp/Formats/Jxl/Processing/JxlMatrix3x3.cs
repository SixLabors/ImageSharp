// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0051 // Remove unused private members

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal struct JxlMatrix3x3
{
    /// <summary>
    /// Represents the matrix element at 0,0.
    /// </summary>
    private double e00;

    /// <summary>
    /// Represents the matrix element at 0,1.
    /// </summary>
    private double e01;

    /// <summary>
    /// Represents the matrix element at 0,2.
    /// </summary>
    private double e02;

    /// <summary>
    /// Represents the matrix element at 1,0.
    /// </summary>
    private double e10;

    /// <summary>
    /// Represents the matrix element at 1,1.
    /// </summary>
    private double e11;

    /// <summary>
    /// Represents the matrix element at 1,2.
    /// </summary>
    private double e12;

    /// <summary>
    /// Represents the matrix element at 2,0.
    /// </summary>
    private double e20;

    /// <summary>
    /// Represents the matrix element at 2,1.
    /// </summary>
    private double e21;

    /// <summary>
    /// Represents the matrix element at 2,2.
    /// </summary>
    private double e22;

    /// <summary>
    /// Wraps all these values into a Span.
    /// </summary>
    /// <returns>A Span with all matrix elements.</returns>
    public Span<double> AsSpan() => MemoryMarshal.CreateSpan(ref this.e00, 9);

    /// <summary>
    /// Wraps all these values into a ReadOnlySpan.
    /// </summary>
    /// <returns>A ReadOnlySpan with all matrix elements.</returns>
    public ReadOnlySpan<double> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref this.e00, 9);

    public static void Multiply(in JxlMatrix3x3 a, in JxlMatrix3x3 b, ref JxlMatrix3x3 c)
    {
        ReadOnlySpan<double> spanA = a.AsReadOnlySpan();
        ReadOnlySpan<double> spanB = b.AsReadOnlySpan();
        Span<double> spanC = c.AsSpan();

        for (int row = 0; row < 3; row++)
        {
            int row3 = row * 3;
            for (int col = 0; col < 3; col++)
            {
                double sum = 0d;
                for (int k = 0; k < 3; k++)
                {
                    sum += spanA[row3 + k] * spanB[(k * 3) + col];
                }

                spanC[row3 + col] = sum;
            }
        }
    }

    public static void Multiply(in JxlMatrix3x3 a, ReadOnlySpan<double> b, Span<double> c)
    {
        ReadOnlySpan<double> spanA = a.AsReadOnlySpan();

        for (int row = 0; row < 3; row++)
        {
            double sum = 0f;
            int row3 = row * 3;
            for (int col = 0; col < 3; col++)
            {
                sum += spanA[row3 + col] * b[col];
            }

            c[row] = sum;
        }
    }

    public static bool Invert(ref JxlMatrix3x3 matrix)
    {
        ReadOnlySpan<double> m = matrix.AsReadOnlySpan();
        Span<double> temp =
        [
            ((double)m[4] * m[8]) - ((double)m[5] * m[7]),
            ((double)m[2] * m[7]) - ((double)m[1] * m[8]),
            ((double)m[1] * m[5]) - ((double)m[2] * m[4]),
            ((double)m[5] * m[6]) - ((double)m[3] * m[8]),
            ((double)m[0] * m[8]) - ((double)m[2] * m[6]),
            ((double)m[2] * m[3]) - ((double)m[0] * m[5]),
            ((double)m[3] * m[7]) - ((double)m[4] * m[6]),
            ((double)m[1] * m[6]) - ((double)m[0] * m[7]),
            ((double)m[0] * m[4]) - ((double)m[1] * m[3]),
        ];

        double det = (m[0] * temp[0]) + (m[1] * temp[3]) + (m[2] * temp[6]);

        if (Math.Abs(det) < 1e-10)
        {
            return false;
        }

        double idet = 1.0 / det;
        Span<double> spanM = matrix.AsSpan();

        for (int i = 0; i < 9; i++)
        {
            spanM[i] = (double)(temp[i] * idet);
        }

        return true;
    }
}
