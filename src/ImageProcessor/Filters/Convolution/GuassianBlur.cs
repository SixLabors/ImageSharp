namespace ImageProcessor.Filters.Convolution
{
    using System;

    public class GuassianBlur : Convolution2DFilter
    {
        private int kernelSize;

        private float standardDeviation;

        private float[,] kernelY;
        private float[,] kernelX;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuassianBlur"/> class.
        /// </summary>
        /// <param name="size">
        /// The size.
        /// </param>
        /// <param name="standardDeviation">
        /// The standard deviation 'sigma' value for calculating Gaussian curves.
        /// </param>
        public GuassianBlur(int size, float standardDeviation = 1.4f)
        {
            this.kernelSize = size;
            this.standardDeviation = standardDeviation;
        }

        /// <inheritdoc/>
        public override float[,] KernelX => this.kernelX;

        /// <inheritdoc/>
        public override float[,] KernelY => this.kernelY;

        /// <inheritdoc/>
        protected override void OnApply(Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.kernelY == null)
            {
                this.kernelY = this.CreateGaussianKernel(false);
            }

            if (this.kernelX == null)
            {
                this.kernelX = this.CreateGaussianKernel(true);
            }
        }

        /// <summary>
        /// Create a 2 dimensional Gaussian kernel using the Gaussian G(x y) function
        /// </summary>
        private void CreateGaussianKernel2D()
        {
            int size = this.kernelSize;
            float[,] kernel = new float[size, size];

            int midpoint = size / 2;
            float sum = 0;
            for (int i = 0; i < size; i++)
            {
                int x = i - midpoint;

                for (int j = 0; j < size; j++)
                {
                    int y = j - midpoint;
                    float gxy = this.Gaussian2D(x, y);
                    sum += gxy;
                    kernel[i, j] = gxy;
                }
            }

            // Normalise kernel so that the sum of all weights equals 1
            //for (int i = 0; i < size; i++)
            //{
            //    for (int j = 0; j < size; j++)
            //    {
            //        kernel[i, 0] = kernel[i, j] / sum;
            //    }
            //}

            this.kernelY = kernel;
        }

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function
        /// </summary>
        /// <returns>The <see cref="T:float[,]"/></returns>
        private float[,] CreateGaussianKernel(bool horizontal)
        {
            int size = this.kernelSize;
            float[,] kernel = horizontal ? new float[1, size] : new float[size, 1];
            float sum = 0.0f;

            int midpoint = size / 2;
            for (int i = 0; i < size; i++)
            {
                int x = i - midpoint;
                float gx = this.Gaussian(x);
                sum += gx;
                if (horizontal)
                {
                    kernel[0, i] = gx;
                }
                else
                {
                    kernel[i, 0] = gx;
                }
            }

            // Normalise kernel so that the sum of all weights equals 1
            if (horizontal)
            {
                for (int i = 0; i < size; i++)
                {
                    kernel[0, i] = kernel[0, i] / sum;
                }
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    kernel[i, 0] = kernel[i, 0] / sum;
                }
            }

            return kernel;
        }

        /// <summary>
        /// Implementation of 1D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x)</param>
        /// <returns>The Gaussian G(x)</returns>
        private float Gaussian(float x)
        {
            const float Numerator = 1.0f;
            float deviation = this.standardDeviation;
            float denominator = (float)(Math.Sqrt(2 * Math.PI) * deviation);

            float exponentNumerator = -x * x;
            float exponentDenominator = (float)(2 * Math.Pow(deviation, 2));

            float left = Numerator / denominator;
            float right = (float)Math.Exp(exponentNumerator / exponentDenominator);

            return left * right;
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
            float deviation = this.standardDeviation;
            double pow = Math.Pow(deviation, 2);
            float denominator = (float)((2 * Math.PI) * pow);

            float exponentNumerator = (-x * x) + (-y * y);
            float exponentDenominator = (float)(2 * pow);

            float left = Numerator / denominator;
            float right = (float)Math.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }
    }
}
