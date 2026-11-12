// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing.Processors.Transforms.Linear;

/// <summary>
/// Solves square systems of linear equations by Gaussian elimination with partial pivoting.
/// </summary>
/// <remarks>
/// Elimination reduces the system to an upper triangular matrix. Back substitution solves one variable at a time from the last row to the first.
/// The algorithm follows the <see href="https://www.algorithm-archive.org/contents/gaussian_elimination/gaussian_elimination.html">Algorithm Archive</see>.
/// </remarks>
internal static class GaussianEliminationSolver
{
    /// <summary>
    /// Solves the system in place.
    /// </summary>
    /// <param name="matrix">The nonempty square matrix of coefficients. Each row represents one equation.</param>
    /// <param name="result">The right-hand side, with one value per row. On success it contains the solution in column order.</param>
    /// <exception cref="NotSupportedException">The current column has no nonzero pivot in the remaining rows.</exception>
    /// <remarks>
    /// On success the matrix contains the upper triangular coefficients. Both arguments can be partly updated if elimination fails.
    /// </remarks>
    public static void Solve(double[][] matrix, double[] result)
    {
        TransformToRowEchelonForm(matrix, result);
        ApplyBackSubstitution(matrix, result);
    }

    /// <summary>
    /// Eliminates the coefficients below each pivot and applies the same row operations to the right-hand side.
    /// </summary>
    /// <param name="matrix">The square coefficient matrix to reduce.</param>
    /// <param name="result">The right-hand side to update with each row operation.</param>
    /// <exception cref="NotSupportedException">The current column has no nonzero pivot in the remaining rows.</exception>
    private static void TransformToRowEchelonForm(double[][] matrix, double[] result)
    {
        int colCount = matrix.Length;
        int rowCount = matrix[0].Length;
        int pivotRow = 0;

        for (int pivotCol = 0; pivotCol < colCount; pivotCol++)
        {
            // Select the largest pivot magnitude. This keeps the magnitude of each elimination multiplier at most one.
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
                throw new NotSupportedException("The matrix is singular.");
            }

            // Move each constant with its coefficient row to keep the equations equivalent.
            (matrix[pivotRow], matrix[maxIndex]) = (matrix[maxIndex], matrix[pivotRow]);
            (result[pivotRow], result[maxIndex]) = (result[maxIndex], result[pivotRow]);

            for (int r = pivotRow + 1; r < rowCount; r++)
            {
                // This multiple of the pivot row cancels the current coefficient. Earlier columns already contain zeros.
                double fraction = matrix[r][pivotCol] / matrix[pivotRow][pivotCol];

                for (int c = pivotCol + 1; c < colCount; c++)
                {
                    matrix[r][c] -= matrix[pivotRow][c] * fraction;
                }

                result[r] -= result[pivotRow] * fraction;

                // Store the exact zero that the row operation represents.
                matrix[r][pivotCol] = 0;
            }

            pivotRow++;
        }
    }

    /// <summary>
    /// Solves the upper triangular system from the last row to the first.
    /// </summary>
    /// <param name="matrix">The upper triangular coefficients with nonzero diagonal entries.</param>
    /// <param name="result">The reduced right-hand side, replaced in place by the solution.</param>
    private static void ApplyBackSubstitution(double[][] matrix, double[] result)
    {
        int rowCount = matrix[0].Length;

        for (int row = rowCount - 1; row >= 0; row--)
        {
            // The later variables are already removed from this equation. Divide by the remaining coefficient.
            result[row] /= matrix[row][row];

            // Remove the solved variable from every earlier equation.
            for (int r = 0; r < row; r++)
            {
                result[r] -= result[row] * matrix[r][row];
            }
        }
    }
}
