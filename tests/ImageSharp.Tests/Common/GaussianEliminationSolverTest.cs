// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Transforms.Linear;

namespace SixLabors.ImageSharp.Tests.Common;

public class GaussianEliminationSolverTest
{
    /// <summary>
    /// Checks the contiguous solver with the existing matrix cases.
    /// </summary>
    /// <param name="matrix">The coefficient rows.</param>
    /// <param name="result">The right-hand side.</param>
    /// <param name="expected">The expected solution.</param>
    [Theory]
    [MemberData(nameof(MatrixTestData))]
    public void CanSolveContiguous(double[][] matrix, double[] result, double[] expected)
    {
        double[] coefficients = matrix.SelectMany(row => row).ToArray();

        Assert.True(GaussianEliminationSolver.TrySolve(coefficients, result));

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i], 4);
        }
    }

    /// <summary>
    /// Checks that dependent equations do not produce a solution.
    /// </summary>
    [Fact]
    public void ContiguousSolverRejectsSingularMatrix()
    {
        double[] matrix = [1D, 2D, 2D, 4D];
        double[] result = [3D, 6D];

        Assert.False(GaussianEliminationSolver.TrySolve(matrix, result));
    }

    [Theory]
    [MemberData(nameof(MatrixTestData))]
    public void CanSolve(double[][] matrix, double[] result, double[] expected)
    {
        GaussianEliminationSolver.Solve(matrix, result);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(result[i], expected[i], 4);
        }
    }

    public static TheoryData<double[][], double[], double[]> MatrixTestData
    {
        get
        {
            TheoryData<double[][], double[], double[]> data = [];
            {
                double[][] matrix =
                [
                    [2, 3, 4],
                    [1, 2, 3],
                    [3, -4, 0],
                ];
                double[] result = [6, 4, 10];
                double[] expected = [18 / 11f, -14 / 11f, 18 / 11f];
                data.Add(matrix, result, expected);
            }

            {
                double[][] matrix =
                [
                    [1, 4, -1],
                    [2, 5, 8],
                    [1, 3, -3],
                ];
                double[] result = [4, 15, 1];
                double[] expected = [1, 1, 1];
                data.Add(matrix, result, expected);
            }

            {
                double[][] matrix =
                [
                    [-1, 0, 0],
                    [0, 1, 0],
                    [0, 0, 1],
                ];
                double[] result = [1, 2, 3];
                double[] expected = [-1, 2, 3];
                data.Add(matrix, result, expected);
            }

            return data;
        }
    }
}
