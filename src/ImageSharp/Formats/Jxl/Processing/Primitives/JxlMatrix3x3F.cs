// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

internal struct JxlMatrix3x3F
{
    /// <summary>
    /// Represents the matrix element at 0,0.
    /// </summary>
    private float e00;

    /// <summary>
    /// Represents the matrix element at 0,1.
    /// </summary>
    private float e01;

    /// <summary>
    /// Represents the matrix element at 0,2.
    /// </summary>
    private float e02;

    /// <summary>
    /// Represents the matrix element at 1,0.
    /// </summary>
    private float e10;

    /// <summary>
    /// Represents the matrix element at 1,1.
    /// </summary>
    private float e11;

    /// <summary>
    /// Represents the matrix element at 1,2.
    /// </summary>
    private float e12;

    /// <summary>
    /// Represents the matrix element at 2,0.
    /// </summary>
    private float e20;

    /// <summary>
    /// Represents the matrix element at 2,1.
    /// </summary>
    private float e21;

    /// <summary>
    /// Represents the matrix element at 2,2.
    /// </summary>
    private float e22;

    internal JxlMatrix3x3F(float[][] array)
    {
        this.e00 = array[0][0];
        this.e01 = array[0][1];
        this.e02 = array[0][2];
        this.e10 = array[1][0];
        this.e11 = array[1][1];
        this.e12 = array[1][2];
        this.e20 = array[2][0];
        this.e21 = array[2][1];
        this.e22 = array[2][2];
    }

    /// <summary>
    /// Wraps all these values into a Span.
    /// </summary>
    /// <returns>A Span with all matrix elements.</returns>
    public Span<float> AsSpan() => MemoryMarshal.CreateSpan(ref this.e00, 9);

    /// <summary>
    /// Wraps all these values into a ReadOnlySpan.
    /// </summary>
    /// <returns>A ReadOnlySpan with all matrix elements.</returns>
    public ReadOnlySpan<float> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref this.e00, 9);

    public static void Multiply(in JxlMatrix3x3F a, in JxlMatrix3x3F b, ref JxlMatrix3x3F c)
    {
        ReadOnlySpan<float> spanA = a.AsReadOnlySpan();
        ReadOnlySpan<float> spanB = b.AsReadOnlySpan();
        Span<float> spanC = c.AsSpan();

        for (int row = 0; row < 3; row++)
        {
            int row3 = row * 3;
            for (int col = 0; col < 3; col++)
            {
                float sum = 0f;
                for (int k = 0; k < 3; k++)
                {
                    sum += spanA[row3 + k] * spanB[(k * 3) + col];
                }

                spanC[row3 + col] = sum;
            }
        }
    }

    public static void Multiply(in JxlMatrix3x3F a, ReadOnlySpan<float> b, Span<float> c)
    {
        ReadOnlySpan<float> spanA = a.AsReadOnlySpan();

        for (int row = 0; row < 3; row++)
        {
            float sum = 0f;
            int row3 = row * 3;
            for (int col = 0; col < 3; col++)
            {
                sum += spanA[row3 + col] * b[col];
            }

            c[row] = sum;
        }
    }

    public static bool Invert(ref JxlMatrix3x3F matrix)
    {
        ReadOnlySpan<float> m = matrix.AsReadOnlySpan();
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
        Span<float> spanM = matrix.AsSpan();

        for (int i = 0; i < 9; i++)
        {
            spanM[i] = (float)(temp[i] * idet);
        }

        return true;
    }
}
