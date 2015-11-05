namespace ImageProcessor.Filters.Convolution
{
    using System;
    using System.Runtime.CompilerServices;

    public class GuassianBlur : ConvolutionFilter
    {
        private int kernelSize;

        private float standardDeviation;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuassianBlur"/> class.
        /// </summary>
        /// <param name="size">
        /// The size.
        /// </param>
        /// <param name="standardDeviation">
        /// The standard deviation 'sigma' value for calculating Gaussian curves.
        /// </param>
        public GuassianBlur(int size, float standardDeviation)
        {
            this.kernelSize = size;
            this.standardDeviation = standardDeviation;
        }

        public override float[,] KernelX { get; }

        /// <summary>
        /// Create a 2 dimensional Gaussian kernel using the Gaussian G(x y) function for 
        /// blurring images.
        /// </summary>
        /// <param name="kernelSize">Kernel Size</param>
        /// <returns>A Gaussian Kernel with the given size.</returns>
        private float[,] CreateGuassianBlurFilter()
        {
            // Create kernel
            int size = this.kernelSize;
            float[,] kernel = this.CreateGaussianKernel2D(size);
            float min = kernel[0, 0];

            // Convert to integer blurring kernel. First of all the integer kernel is calculated from Kernel2D
            // by dividing all elements by the element with the smallest value.
            float[,] intKernel = new float[size, size];
            int divider = 0;

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float v = kernel[i, j] / min;

                    if (v > ushort.MaxValue)
                    {
                        v = ushort.MaxValue;
                    }

                    intKernel[i, j] = (int)v;

                    // Collect the divider
                    divider += (int)intKernel[i, j];
                }
            }

            // Update filter
            //this.Divider = divider;
            return intKernel;
        }

        /// <summary>
        /// Create a 2 dimensional Gaussian kernel using the Gaussian G(x y) function
        /// </summary>
        /// <param name="kernelSize">Kernel Size</param>
        /// <returns>A Gaussian Kernel with the given size and deviation.</returns>
        public float[,] CreateGaussianKernel2D(int kernelSize)
        {
            float[,] kernel = new float[kernelSize, kernelSize];

            int midpoint = kernelSize / 2;

            for (int i = 0; i < kernelSize; i++)
            {
                int x = i - midpoint;

                for (int j = 0; j < kernelSize; j++)
                {
                    int y = j - midpoint;
                    float gxy = this.Gaussian2D(x, y);
                    kernel[i, j] = gxy;
                }
            }

            return kernel;
        }

        /// <summary>
        /// Implementation of 2D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x y)</param>
        /// <param name="y">The y provided to G(x y)</param>
        /// <returns>The Gaussian G(x y)</returns>
        private float Gaussian2D(float x, float y)
        {
            const float Numerator = 1.0f;
            float denominator = (float)((2 * Math.PI) * Math.Pow(this.standardDeviation, 2));

            float exponentNumerator = (-x * x) + (-y * y);
            float exponentDenominator = (float)(2 * Math.Pow(this.standardDeviation, 2));

            float left = Numerator / denominator;
            float right = (float)Math.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }
    }
}
