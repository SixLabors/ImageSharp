// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing.Processors.Transforms.Linear;

/// <summary>
/// Represents a solver for systems of linear equations using the Gaussian Elimination method.
/// This class applies Gaussian Elimination to transform the matrix into row echelon form and then performs back substitution to find the solution vector.
/// This implementation is based on: <see href="https://www.algorithm-archive.org/contents/gaussian_elimination/gaussian_elimination.html"/>
/// </summary>
internal static class GaussianEliminationSolver
{
    /// <summary>
    /// Solves the system of linear equations represented by the given matrix and result vector using Gaussian Elimination.
    /// </summary>
    /// <param name="matrix">The square matrix representing the coefficients of the linear equations.</param>
    /// <param name="result">The vector representing the constants on the right-hand side of the linear equations.</param>
    /// <exception cref="Exception">Thrown if the matrix is singular and cannot be solved.</exception>
    /// <remarks>
    /// The matrix passed to this method must be a square matrix.
    /// If the matrix is singular (i.e., has no unique solution), an <see cref="NotSupportedException"/> will be thrown.
    /// </remarks>
    public static void Solve(double[][] matrix, double[] result)
    {
        TransformToRowEchelonForm(matrix, result);
        ApplyBackSubstitution(matrix, result);
    }

    private static void TransformToRowEchelonForm(double[][] matrix, double[] result)
    {
        int colCount = matrix.Length;
        int rowCount = matrix[0].Length;
        int pivotRow = 0;
        for (int pivotCol = 0; pivotCol < colCount; pivotCol++)
        {
            double maxValue = double.Abs(matrix[pivotRow][pivotCol]);
            int maxIndex = pivotRow;
            for (int r = pivotRow + 1; r < rowCount; r++)
            {
                double value = double.Abs(matrix[r][pivotCol]);
                if (value > maxValue)
                {
                    maxIndex = r;
                    maxValue = value;
                }
            }

            if (matrix[maxIndex][pivotCol] == 0)
            {
                throw new NotSupportedException("Matrix is singular and cannot be solve");
            }

            (matrix[pivotRow], matrix[maxIndex]) = (matrix[maxIndex], matrix[pivotRow]);
            (result[pivotRow], result[maxIndex]) = (result[maxIndex], result[pivotRow]);

            for (int r = pivotRow + 1; r < rowCount; r++)
            {
                double fraction = matrix[r][pivotCol] / matrix[pivotRow][pivotCol];
                for (int c = pivotCol + 1; c < colCount; c++)
                {
                    matrix[r][c] -= matrix[pivotRow][c] * fraction;
                }

                result[r] -= result[pivotRow] * fraction;
                matrix[r][pivotCol] = 0;
            }

            pivotRow++;
        }
    }

    private static void ApplyBackSubstitution(double[][] matrix, double[] result)
    {
        int rowCount = matrix[0].Length;

        for (int row = rowCount - 1; row >= 0; row--)
        {
            result[row] /= matrix[row][row];

            for (int r = 0; r < row; r++)
            {
                result[r] -= result[row] * matrix[r][row];
            }
        }
    }
}
