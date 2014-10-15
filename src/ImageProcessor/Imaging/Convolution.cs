// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Convolution.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides methods for applying blurring and sharpening effects to an image..
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    using System;
    using System.Drawing;
    using System.Threading.Tasks;

    using ImageProcessor.Common.Extensions;

    /// <summary>
    /// Provides methods for applying blurring and sharpening effects to an image..
    /// </summary>
    public class Convolution
    {
        /// <summary>
        /// The standard deviation 'sigma' value for calculating Gaussian curves.
        /// </summary>
        private readonly double standardDeviation = 1.4;

        /// <summary>
        /// Whether to use dynamic divider for edges.
        /// </summary>
        private bool useDynamicDividerForEdges = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution"/> class.
        /// </summary>
        public Convolution()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution"/> class.
        /// </summary>
        /// <param name="standardDeviation">
        /// The standard deviation.
        /// </param>
        public Convolution(double standardDeviation)
        {
            this.standardDeviation = standardDeviation;
        }

        /// <summary>
        /// Gets or sets the threshold to add to the weighted sum.
        /// <remarks>
        /// <para>
        /// Specifies the threshold value, which is added to each weighted
        /// sum of pixels.
        /// </para>
        /// </remarks>
        /// </summary>
        public int Threshold { get; set; }

        /// <summary>
        /// Gets or sets the value used to divide convolution; the weighted sum
        /// of pixels is divided by this value.
        /// <remarks>
        /// <para>If not set this value will be automatically calculated.</para>
        /// </remarks>
        /// </summary>
        public double Divider { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the dynamic divider for edges.
        /// <remarks>
        /// <para>
        /// If it is set to <see langword="false" />, then the same divider, specified by <see cref="Divider" />
        /// property or calculated automatically, will be applied both for non-edge regions
        /// and for edge regions. If the value is set to <see langword="true" />, then the dynamically
        /// calculated divider will be used for edge regions. This is calculated from the sum of the kernel
        /// elements used at that region, which are taken into account for particular processed pixel
        /// (elements, which are not outside image).
        /// </para>
        /// <para>Default value is set to <see langword="true" />.</para>
        /// </remarks>
        /// </summary>
        public bool UseDynamicDividerForEdges
        {
            get
            {
                return this.useDynamicDividerForEdges;
            }

            set
            {
                this.useDynamicDividerForEdges = value;
            }
        }

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function
        /// </summary>
        /// <param name="kernelSize">Kernel Size</param>
        /// <returns>A Gaussian Kernel with the given size and deviation.</returns>
        public double[,] CreateGaussianKernel(int kernelSize)
        {
            double[,] kernel = new double[kernelSize, 1];
            double sum = 0.0d;

            int midpoint = kernelSize / 2;
            for (int i = 0; i < kernelSize; i++)
            {
                int x = i - midpoint;
                double gx = this.Gaussian(x);
                sum += gx;
                kernel[i, 0] = gx;
            }

            // Normalise kernel so that the sum of all weights equals 1
            for (int i = 0; i < kernelSize; i++)
            {
                kernel[i, 0] = kernel[i, 0] / sum;
            }

            return kernel;
        }

        /// <summary>
        /// Create a 2 dimensional Gaussian kernel using the Gaussian G(x y) function
        /// </summary>
        /// <param name="kernelSize">Kernel Size</param>
        /// <returns>A Gaussian Kernel with the given size and deviation.</returns>
        public double[,] CreateGaussianKernel2D(int kernelSize)
        {
            double[,] kernel = new double[kernelSize, kernelSize];

            int midpoint = kernelSize / 2;

            for (int i = 0; i < kernelSize; i++)
            {
                int x = i - midpoint;

                for (int j = 0; j < kernelSize; j++)
                {
                    int y = j - midpoint;
                    double gxy = this.Gaussian2D(x, y);
                    kernel[i, j] = gxy;
                }
            }

            return kernel;
        }

        /// <summary>
        /// Create a 2 dimensional Gaussian kernel using the Gaussian G(x y) function for 
        /// blurring images.
        /// </summary>
        /// <param name="kernelSize">Kernel Size</param>
        /// <returns>A Gaussian Kernel with the given size.</returns>
        public double[,] CreateGuassianBlurFilter(int kernelSize)
        {
            // Create kernel
            double[,] kernel = this.CreateGaussianKernel2D(kernelSize);
            double min = kernel[0, 0];

            // Convert to integer blurring kernel. First of all the integer kernel is calculated from Kernel2D
            // by dividing all elements by the element with the smallest value.
            double[,] intKernel = new double[kernelSize, kernelSize];
            int divider = 0;

            for (int i = 0; i < kernelSize; i++)
            {
                for (int j = 0; j < kernelSize; j++)
                {
                    double v = kernel[i, j] / min;

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
            this.Divider = divider;
            return intKernel;
        }

        /// <summary>
        /// Create a 2 dimensional Gaussian kernel using the Gaussian G(x y) function for 
        /// sharpening images.
        /// </summary>
        /// <param name="kernelSize">Kernel Size</param>
        /// <returns>A Gaussian Kernel with the given size.</returns>
        public double[,] CreateGuassianSharpenFilter(int kernelSize)
        {
            // Create kernel
            double[,] kernel = this.CreateGaussianKernel2D(kernelSize);
            double min = kernel[0, 0];

            // integer kernel
            double[,] intKernel = new double[kernelSize, kernelSize];
            int sum = 0;
            int divider = 0;

            for (int i = 0; i < kernelSize; i++)
            {
                for (int j = 0; j < kernelSize; j++)
                {
                    double v = kernel[i, j] / min;

                    if (v > ushort.MaxValue)
                    {
                        v = ushort.MaxValue;
                    }

                    intKernel[i, j] = (int)v;

                    // Collect the sum.
                    sum += (int)intKernel[i, j];
                }
            }

            // Recalculate the kernel.
            int center = kernelSize >> 1;

            for (int i = 0; i < kernelSize; i++)
            {
                for (int j = 0; j < kernelSize; j++)
                {
                    if ((i == center) && (j == center))
                    {
                        // Calculate central value
                        intKernel[i, j] = (2 * sum) - intKernel[i, j];
                    }
                    else
                    {
                        // invert value
                        intKernel[i, j] = -intKernel[i, j];
                    }

                    // Collect the divider
                    divider += (int)intKernel[i, j];
                }
            }

            // Update filter
            this.Divider = divider;
            return intKernel;
        }

        /// <summary>
        /// Processes the given kernel to produce an array of pixels representing a bitmap.
        /// </summary>
        /// <param name="source">The image to process.</param>
        /// <param name="kernel">The Gaussian kernel to use when performing the method</param>
        /// <returns>A processed bitmap.</returns>
        public Bitmap ProcessKernel(Bitmap source, double[,] kernel)
        {
            int width = source.Width;
            int height = source.Height;
            Bitmap destination = new Bitmap(width, height);
            destination.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            using (FastBitmap sourceBitmap = new FastBitmap(source))
            {
                using (FastBitmap destinationBitmap = new FastBitmap(destination))
                {
                    int kernelLength = kernel.GetLength(0);
                    int radius = kernelLength >> 1;
                    int kernelSize = kernelLength * kernelLength;
                    int threshold = this.Threshold;

                    // For each line
                    Parallel.For(
                        0,
                        height,
                        y =>
                        {
                            // For each pixel
                            for (int x = 0; x < width; x++)
                            {
                                // The number of kernel elements taken into account
                                int processedKernelSize;

                                // Colour sums
                                double blue;
                                double alpha;
                                double divider;
                                double green;
                                double red = green = blue = alpha = divider = processedKernelSize = 0;

                                // For each kernel row
                                for (int i = 0; i < kernelLength; i++)
                                {
                                    int ir = i - radius;
                                    int offsetY = y + ir;

                                    // Skip the current row
                                    if (offsetY < 0)
                                    {
                                        continue;
                                    }

                                    // Outwith the current bounds so break.
                                    if (offsetY >= height)
                                    {
                                        break;
                                    }

                                    // For each kernel column
                                    for (int j = 0; j < kernelLength; j++)
                                    {
                                        int jr = j - radius;
                                        int offsetX = x + jr;

                                        // Skip the column
                                        if (offsetX < 0)
                                        {
                                            continue;
                                        }

                                        if (offsetX < width)
                                        {
                                            // ReSharper disable once AccessToDisposedClosure
                                            Color color = sourceBitmap.GetPixel(offsetX, offsetY);
                                            double k = kernel[i, j];
                                            divider += k;

                                            red += k * color.R;
                                            green += k * color.G;
                                            blue += k * color.B;
                                            alpha += k * color.A;

                                            processedKernelSize++;
                                        }
                                    }
                                }

                                // Check to see if all kernel elements were processed
                                if (processedKernelSize == kernelSize)
                                {
                                    // All kernel elements are processed; we are not on the edge.
                                    divider = this.Divider;
                                }
                                else
                                {
                                    // We are on an edge; do we need to use dynamic divider or not?
                                    if (!this.UseDynamicDividerForEdges)
                                    {
                                        // Apply the set divider.
                                        divider = this.Divider;
                                    }
                                }

                                // Check and apply the divider
                                if ((long)divider != 0)
                                {
                                    red /= divider;
                                    green /= divider;
                                    blue /= divider;
                                    alpha /= divider;
                                }

                                // Add any applicable threshold.
                                red += threshold;
                                green += threshold;
                                blue += threshold;
                                alpha += threshold;

                                // ReSharper disable once AccessToDisposedClosure
                                destinationBitmap.SetPixel(x, y, Color.FromArgb(alpha.ToByte(), red.ToByte(), green.ToByte(), blue.ToByte()));
                            }
                        });
                }
            }

            return destination;
        }

        #region Private
        /// <summary>
        /// Implementation of 1D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x)</param>
        /// <returns>The Gaussian G(x)</returns>
        private double Gaussian(double x)
        {
            const double Numerator = 1.0;
            double denominator = Math.Sqrt(2 * Math.PI) * this.standardDeviation;

            double exponentNumerator = -x * x;
            double exponentDenominator = 2 * Math.Pow(this.standardDeviation, 2);

            double left = Numerator / denominator;
            double right = Math.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }

        /// <summary>
        /// Implementation of 2D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x y)</param>
        /// <param name="y">The y provided to G(x y)</param>
        /// <returns>The Gaussian G(x y)</returns>
        private double Gaussian2D(double x, double y)
        {
            const double Numerator = 1.0;
            double denominator = (2 * Math.PI) * Math.Pow(this.standardDeviation, 2);

            double exponentNumerator = (-x * x) + (-y * y);
            double exponentDenominator = 2 * Math.Pow(this.standardDeviation, 2);

            double left = Numerator / denominator;
            double right = Math.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }
        #endregion
    }
}
