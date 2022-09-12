// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    internal static class ConvolutionProcessorHelpers
    {
        /// <summary>
        /// Kernel radius is calculated using the minimum viable value.
        /// See <see href="http://chemaguerra.com/gaussian-filter-radius/"/>.
        /// </summary>
        /// <param name="sigma">The weight of the blur.</param>
        internal static int GetDefaultGaussianRadius(float sigma)
            => (int)MathF.Ceiling(sigma * 3);

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function.
        /// </summary>
        /// <returns>The convolution kernel.</returns>
        /// <param name="size">The kernel size.</param>
        /// <param name="weight">The weight of the blur.</param>
        internal static float[] CreateGaussianBlurKernel(int size, float weight)
        {
            float[] kernel = new float[size];

            float sum = 0F;
            float midpoint = (size - 1) / 2F;

            for (int i = 0; i < size; i++)
            {
                float x = i - midpoint;
                float gx = Numerics.Gaussian(x, weight);
                sum += gx;
                kernel[i] = gx;
            }

            // Normalize kernel so that the sum of all weights equals 1
            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
        }

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function
        /// </summary>
        /// <param name="size">The kernel size.</param>
        /// <param name="weight">The weight of the blur.</param>
        /// <returns>The convolution kernel.</returns>
        internal static float[] CreateGaussianSharpenKernel(int size, float weight)
        {
            float[] kernel = new float[size];

            float sum = 0;

            float midpoint = (size - 1) / 2F;
            for (int i = 0; i < size; i++)
            {
                float x = i - midpoint;
                float gx = Numerics.Gaussian(x, weight);
                sum += gx;
                kernel[i] = gx;
            }

            // Invert the kernel for sharpening.
            int midpointRounded = (int)midpoint;
            for (int i = 0; i < size; i++)
            {
                if (i == midpointRounded)
                {
                    // Calculate central value
                    kernel[i] = (2F * sum) - kernel[i];
                }
                else
                {
                    // invert value
                    kernel[i] = -kernel[i];
                }
            }

            // Normalize kernel so that the sum of all weights equals 1
            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
        }

        /// <summary>
        /// Checks whether or not a given NxM matrix is linearly separable, and if so, it extracts the separable components.
        /// These would be two 1D vectors, <paramref name="row"/> of size N and <paramref name="column"/> of size M.
        /// This algorithm runs in O(NM).
        /// </summary>
        /// <param name="matrix">The input 2D matrix to analyze.</param>
        /// <param name="row">The resulting 1D row vector, if possible.</param>
        /// <param name="column">The resulting 1D column vector, if possible.</param>
        /// <returns>Whether or not <paramref name="matrix"/> was linearly separable.</returns>
        public static bool TryGetLinearlySeparableComponents(this DenseMatrix<float> matrix, out float[] row, out float[] column)
        {
            int height = matrix.Rows;
            int width = matrix.Columns;

            float[] tempX = new float[width];
            float[] tempY = new float[height];

            // This algorithm checks whether the input matrix is linearly separable and extracts two
            // 1D components if possible. Note that for a given NxM matrix that is linearly separable,
            // there exists an infinite number of possible solutions to the system of linear equations
            // representing the possible 1D components that can produce the input matrix as a product.
            // Let's assume we have a 3x3 input matrix to describe the logic. We have the following:
            //
            //     | m11, m12, m13 |                                                  | c1 |
            // M = | m21, m22, m23 |, and we want to find: R = | r1, r2, r3 | and C = | c2 |.
            //     | m31, m32, m33 |                                                  | c3 |
            //
            // We essentially get the following system of linear equations to solve:
            //
            //   / a11 = r1c1
            //   | a12 = r2c1
            //   | a13 = r3c1
            //   | a21 = r1c2                   a11     a12     a13       a11     a12     a13
            //   / a22 = r2c2, which gives us: ----- = ----- = ----- and ----- = ----- = -----.
            //   \ a23 = r3c2                   a21     a22     a23       a31     a32     a33
            //   | a31 = r1c3
            //   | a32 = r2c3
            //   \ a33 = r3c3
            //
            // As we said, there are infinite solutions to this problem (provided the input matrix is in
            // fact linearly separable), but we can look at the equalities above to find a way to define
            // one specific solution that is very easy to calculate (and that is equivalent to all others
            // anyway). In particular, we can see that in order for it to be linearly separable, the matrix
            // needs to have each row linearly dependent on each other. That is, its rank is just 1. This
            // means that we can express the whole matrix as a function of one row vector (any of the rows
            // in the matrix), and a column vector that represents the ratio of each element in a given column
            // j with the corresponding j-th item in the reference row. This same procedure extends naturally
            // to lineary separable 2D matrices of any size, too. So we end up with the following generalized
            // solution for a matrix M of size NxN (or MxN, that works too) and the R and C vectors:
            //
            //     | m11, m12, m13, ..., m1N |                                       | m11/m11 |
            //     | m21, m22, m23, ..., m2N |                                       | m21/m11 |
            // M = | m31, m32, m33, ..., m3N |, R = | m11, m12, m13, ..., m1N |, C = | m31/m11 |.
            //     | ...  ...  ...  ...  ... |                                       |   ...   |
            //     | mN1, mN2, mN3, ..., mNN |                                       | mN1/m11 |
            //
            // So what this algorithm does is just the following:
            //   1) It calculates the C[i] value for each i-th row.
            //   2) It checks that every j-th item in the row respects C[i] = M[i, j] / M[0, j]. If this is
            //      not true for any j-th item in any i-th row, then the matrix is not linearly separable.
            //   3) It sets items in R and C to the values detailed above if the validation passed.
            for (int y = 1; y < height; y++)
            {
                float ratio = matrix[y, 0] / matrix[0, 0];

                for (int x = 1; x < width; x++)
                {
                    if (Math.Abs(ratio - (matrix[y, x] / matrix[0, x])) > 0.0001f)
                    {
                        row = null;
                        column = null;

                        return false;
                    }
                }

                tempY[y] = ratio;
            }

            // The first row is used as a reference, to the ratio is just 1
            tempY[0] = 1;

            // The row component is simply the reference row in the input matrix.
            // In this case, we're just using the first one for simplicity.
            for (int x = 0; x < width; x++)
            {
                tempX[x] = matrix[0, x];
            }

            row = tempX;
            column = tempY;

            return true;
        }
    }
}
