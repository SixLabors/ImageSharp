// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.TestUtils;

/// <summary>
/// This is a naive Discrete Cosine Transform, without any optimization.
/// It is a reference to test against the optimized DCT.
/// </summary>
internal static class TestDCT
{
    private static double Alpha(int u) => u == 0 ? 0.7071067811865475 : 1.0;

    public static void Dct1D(ReadOnlySpan<double> block, Span<double> output, int n, int m)
    {
        Span<double> matrix = stackalloc double[n * n];
        double scale = Math.Sqrt(2.0) / n;

        for (int y = 0; y < n; y++)
        {
            for (int u = 0; u < n; u++)
            {
                matrix[(n * u) + y] =
                    Alpha(u) *
                    Math.Cos((y + 0.5) * u * Math.PI / n) *
                    scale;
            }
        }

        for (int x = 0; x < m; x++)
        {
            for (int u = 0; u < n; u++)
            {
                output[(m * u) + x] = 0;

                for (int y = 0; y < n; y++)
                {
                    output[(m * u) + x] += matrix[(n * u) + y] * block[(m * y) + x];
                }
            }
        }
    }

    public static void Idct1D(ReadOnlySpan<double> block, Span<double> output, int n, int m)
    {
        Span<double> matrix = stackalloc double[n * n];
        double scale = Math.Sqrt(2.0);

        for (int y = 0; y < n; y++)
        {
            for (int u = 0; u < n; u++)
            {
                matrix[(n * y) + u] =
                    Alpha(u) *
                    Math.Cos((y + 0.5) * u * Math.PI / n) *
                    scale;
            }
        }

        for (int x = 0; x < m; x++)
        {
            for (int u = 0; u < n; u++)
            {
                output[(m * u) + x] = 0;

                for (int y = 0; y < n; y++)
                {
                    output[(m * u) + x] += matrix[(n * u) + y] * block[(m * y) + x];
                }
            }
        }
    }

    public static void TransposeBlock(ReadOnlySpan<double> input, Span<double> output, int n, int m)
    {
        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < m; y++)
            {
                output[(y * n) + x] = input[(x * m) + y];
            }
        }
    }

    public static void DctSlow(Span<double> block, int n)
    {
        int blockSize = n * n;
        Span<double> g = stackalloc double[blockSize];

        Dct1D(block, g, n, n);
        TransposeBlock(g, block, n, n);

        Dct1D(block, g, n, n);
        TransposeBlock(g, block, n, n);
    }

    public static void IdctSlow(Span<double> block, int n)
    {
        int blockSize = n * n;
        Span<double> g = stackalloc double[blockSize];

        Idct1D(block, g, n, n);
        TransposeBlock(g, block, n, n);

        Idct1D(block, g, n, n);
        TransposeBlock(g, block, n, n);
    }
}
