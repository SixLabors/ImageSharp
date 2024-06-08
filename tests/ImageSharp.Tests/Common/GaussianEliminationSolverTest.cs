// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Tests.Common;

public class GaussianEliminationSolverTest
{
    [Theory]
    [MemberData(nameof(MatrixTestData))]
    public void CanSolve(float[][] matrix, float[] result, float[] expected)
    {
        GaussianEliminationSolver<float>.Solve(matrix, result);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(result[i], expected[i], 4);
        }
    }

    public static TheoryData<float[][], float[], float[]> MatrixTestData
    {
        get
        {
            TheoryData<float[][], float[], float[]> data = [];
            {
                float[][] matrix =
                [
                    [2, 3, 4],
                    [1, 2, 3],
                    [3, -4, 0],
                ];
                float[] result = [6, 4, 10];
                float[] expected = [18 / 11f, -14 / 11f, 18 / 11f];
                data.Add(matrix, result, expected);
            }

            {
                float[][] matrix =
                [
                    [1, 4, -1],
                    [2, 5, 8],
                    [1, 3, -3],
                ];
                float[] result = [4, 15, 1];
                float[] expected = [1, 1, 1];
                data.Add(matrix, result, expected);
            }

            {
                float[][] matrix =
                [
                    [-1, 0, 0],
                    [0, 1, 0],
                    [0, 0, 1],
                ];
                float[] result = [1, 2, 3];
                float[] expected = [-1, 2, 3];
                data.Add(matrix, result, expected);
            }

            return data;
        }
    }
}
