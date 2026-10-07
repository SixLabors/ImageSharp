// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Fits normalized symmetric Wiener filters using fixed-point alternating least squares.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// The working scale of a fitted Wiener coefficient before quantization to a transmitted tap.
    /// </summary>
    private const int WienerCoefficientScale = 1 << 16;

    /// <summary>
    /// Fits the horizontal and vertical filters and converts them to their transmitted representation.
    /// </summary>
    /// <param name="window">The five- or seven-tap fitting window.</param>
    /// <param name="correlation">The source-to-reconstruction correlations.</param>
    /// <param name="covariance">The symmetric reconstruction covariance matrix.</param>
    /// <returns>The fitted Wiener filter.</returns>
    private static Av1LoopRestorationUnit FitWiener(int window, ReadOnlySpan<long> correlation, ReadOnlySpan<long> covariance)
    {
        ReadOnlySpan<int> initial = [3, -7, 15, 106, 15, -7, 3];
        InlineArray8<int> verticalStorage = default;
        InlineArray8<int> horizontalStorage = default;
        Span<int> vertical = verticalStorage;
        Span<int> horizontal = horizontalStorage;
        int inset = (7 - window) >> 1;
        for (int index = 0; index < window; index++)
        {
            vertical[index] = horizontal[index] = (WienerCoefficientScale / 128) * initial[index + inset];
        }

        // Each update holds one axis fixed. Four alternating rounds preserve the normalization
        // constraint inside the solve, rather than rescaling independently rounded taps afterward.
        for (int iteration = 1; iteration < 5; iteration++)
        {
            UpdateWienerAxis(window, correlation, covariance, vertical, horizontal, true);
            UpdateWienerAxis(window, correlation, covariance, horizontal, vertical, false);
        }

        Av1LoopRestorationUnit result = default;
        result.FilterType = Av1RestorationFilterType.Wiener;
        QuantizeWienerAxis(window, vertical, result.WienerVertical);
        QuantizeWienerAxis(window, horizontal, result.WienerHorizontal);
        return result;
    }

    /// <summary>
    /// Updates one symmetric axis while keeping the other axis fixed.
    /// </summary>
    /// <param name="window">The fitting window width.</param>
    /// <param name="correlation">The source correlations in column-major neighborhood order.</param>
    /// <param name="covariance">The neighborhood covariance matrix.</param>
    /// <param name="axis">The fitted axis to update.</param>
    /// <param name="fixedAxis">The axis held fixed during this solve.</param>
    /// <param name="vertical">Whether the updated axis is vertical.</param>
    private static void UpdateWienerAxis(
        int window,
        ReadOnlySpan<long> correlation,
        ReadOnlySpan<long> covariance,
        Span<int> axis,
        ReadOnlySpan<int> fixedAxis,
        bool vertical)
    {
        InlineArray8<long> solutionStorage = default;
        InlineArray4<long> rightHandSideStorage = default;
        InlineArray16<long> matrixStorage = default;
        InlineArray8<int> integerPartStorage = default;
        InlineArray8<int> fractionalPartStorage = default;
        Span<long> solution = solutionStorage;
        Span<long> rightHandSide = rightHandSideStorage;
        Span<long> matrix = matrixStorage;
        Span<int> integerPart = integerPartStorage;
        Span<int> fractionalPart = fractionalPartStorage;
        int half = (window >> 1) + 1;
        int square = window * window;
        for (int i = 0; i < window; i++)
        {
            integerPart[i] = fixedAxis[i] / WienerCoefficientScale;
            fractionalPart[i] = fixedAxis[i] - (integerPart[i] * WienerCoefficientScale);
            for (int j = 0; j < window; j++)
            {
                int index = vertical ? j : i;
                int folded = index >= half ? window - 1 - index : index;
                rightHandSide[folded] += correlation[(i * window) + j] * fixedAxis[vertical ? i : j] / WienerCoefficientScale;
            }
        }

        for (int i = 0; i < window; i++)
        {
            int foldedI = i >= half ? window - 1 - i : i;
            for (int j = 0; j < window; j++)
            {
                int foldedJ = j >= half ? window - 1 - j : j;
                for (int k = 0; k < window; k++)
                {
                    int foldedK = k >= half ? window - 1 - k : k;
                    for (int l = 0; l < window; l++)
                    {
                        int foldedL = l >= half ? window - 1 - l : l;
                        int covarianceRow = ((vertical ? j : i) * window) + k;
                        int covarianceColumn = ((vertical ? i : j) * window) + l;
                        int firstWeight = vertical ? i : k;
                        int secondWeight = vertical ? j : l;
                        int outputIndex = vertical ? (foldedL * half) + foldedK : (foldedJ * half) + foldedI;
                        long product = covariance[(covarianceRow * square) + covarianceColumn] * fixedAxis[firstWeight] / WienerCoefficientScale;

                        // Split the second multiplication at the coefficient scale. Multiplying two
                        // full-scale coefficients first can overflow even though the scaled sum fits.
                        matrix[outputIndex] += (product * integerPart[secondWeight]) +
                            (product * fractionalPart[secondWeight] / WienerCoefficientScale);
                    }
                }
            }
        }

        int center = half - 1;
        for (int i = 0; i < center; i++)
        {
            rightHandSide[i] -= (rightHandSide[center] * 2) + matrix[(i * half) + center] - (2 * matrix[(center * half) + center]);
        }

        for (int i = 0; i < center; i++)
        {
            for (int j = 0; j < center; j++)
            {
                matrix[(i * half) + j] -= 2 *
                    (matrix[(i * half) + center] + matrix[(center * half) + j] - (2 * matrix[(center * half) + center]));
            }
        }

        if (SolveWienerSystem(center, matrix, half, rightHandSide, solution))
        {
            solution[center] = WienerCoefficientScale;
            for (int i = half; i < window; i++)
            {
                solution[i] = solution[window - 1 - i];
                solution[center] -= 2 * solution[i];
            }

            for (int i = 0; i < window; i++)
            {
                axis[i] = (int)Math.Clamp(solution[i], -(1L << 29), (1L << 29) - 1);
            }
        }
    }

    /// <summary>
    /// Solves the constrained system with integer partial pivoting and bounded elimination scaling.
    /// </summary>
    /// <param name="count">The number of independent symmetric taps.</param>
    /// <param name="matrix">The mutable system matrix.</param>
    /// <param name="stride">The matrix row stride.</param>
    /// <param name="rightHandSide">The mutable system right-hand side.</param>
    /// <param name="solution">The fitted coefficients at the working scale.</param>
    /// <returns>Whether the system has nonzero pivots.</returns>
    private static bool SolveWienerSystem(int count, Span<long> matrix, int stride, Span<long> rightHandSide, Span<long> solution)
    {
        for (int column = 0; column < count - 1; column++)
        {
            for (int row = count - 1; row > column; row--)
            {
                if (Math.Abs(matrix[((row - 1) * stride) + column]) < Math.Abs(matrix[(row * stride) + column]))
                {
                    for (int index = 0; index < count; index++)
                    {
                        (matrix[(row * stride) + index], matrix[((row - 1) * stride) + index]) =
                            (matrix[((row - 1) * stride) + index], matrix[(row * stride) + index]);
                    }

                    (rightHandSide[row], rightHandSide[row - 1]) = (rightHandSide[row - 1], rightHandSide[row]);
                }
            }

            long maximum = 0;
            for (int index = 0; index < count; index++)
            {
                maximum = Math.Max(maximum, Math.Abs(matrix[(column * stride) + index]));
            }

            // A pivot row with small values does the elimination in integers. A row with large values can overflow the products.
            // So that row does the elimination in double precision, and truncates each update. Reference: the scale_threshold branches of linsolve_wiener().
            if (maximum < 1 << 22)
            {
                for (int row = column; row < count - 1; row++)
                {
                    long divisor = matrix[(column * stride) + column];
                    if (divisor == 0)
                    {
                        return false;
                    }

                    long multiplier = matrix[((row + 1) * stride) + column];
                    for (int index = 0; index < count; index++)
                    {
                        matrix[((row + 1) * stride) + index] -= matrix[(column * stride) + index] * multiplier / divisor;
                    }

                    rightHandSide[row + 1] -= multiplier * rightHandSide[column] / divisor;
                }
            }
            else
            {
                for (int row = column; row < count - 1; row++)
                {
                    if (matrix[(column * stride) + column] == 0)
                    {
                        return false;
                    }

                    double multiplier = matrix[((row + 1) * stride) + column];
                    double divisor = matrix[(column * stride) + column];
                    for (int index = 0; index < count; index++)
                    {
                        matrix[((row + 1) * stride) + index] -= (long)(matrix[(column * stride) + index] * multiplier / divisor);
                    }

                    rightHandSide[row + 1] -= (long)(multiplier * rightHandSide[column] / divisor);
                }
            }
        }

        for (int row = count - 1; row >= 0; row--)
        {
            long divisor = matrix[(row * stride) + row];
            if (divisor == 0)
            {
                return false;
            }

            long sum = 0;
            for (int column = row + 1; column < count; column++)
            {
                sum += matrix[(row * stride) + column] * solution[column] / WienerCoefficientScale;
            }

            solution[row] = WienerCoefficientScale * (rightHandSide[row] - sum) / divisor;
        }

        return true;
    }

    /// <summary>
    /// Rounds a fitted symmetric axis into its three transmitted taps.
    /// </summary>
    /// <param name="window">The fitting window width.</param>
    /// <param name="axis">The fitted full symmetric axis.</param>
    /// <param name="taps">The three coded taps, ordered from outermost to innermost.</param>
    private static void QuantizeWienerAxis(int window, ReadOnlySpan<int> axis, Span<int> taps)
    {
        ReadOnlySpan<int> minimum = [-5, -23, -17];
        ReadOnlySpan<int> maximum = [10, 8, 46];
        int inset = (7 - window) >> 1;
        taps[0] = 0;
        for (int index = 0; index < window >> 1; index++)
        {
            int tap = index + inset;
            int rounded = (int)DivideRounded((long)axis[index] * 128, WienerCoefficientScale);
            taps[tap] = Math.Clamp(rounded, minimum[tap], maximum[tap]);
        }
    }
}
