// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies bokeh blur processing to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BokehBlurProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The maximum size of the kernel in either direction.
        /// </summary>
        private readonly int kernelSize;

        /// <summary>
        /// The number of components to use when applying the bokeh blur
        /// </summary>
        private readonly int componentsCount;

        /// <summary>
        /// The kernel components to use for the current instance (a: X, b: Y, A: Z, B: W)
        /// </summary>
        private readonly IReadOnlyList<Vector4> kernelParameters;

        /// <summary>
        /// The scaling factor for kernel values
        /// </summary>
        private readonly float kernelsScale;

        /// <summary>
        /// The mapping of initialized complex kernels and parameters, to speed up the initialization of new <see cref="BokehBlurProcessor{TPixel}"/> instances
        /// </summary>
        private static readonly Dictionary<(int, int), (IReadOnlyList<Vector4>, float, IReadOnlyList<DenseMatrix<Complex64>>)> Cache =
            new Dictionary<(int, int), (IReadOnlyList<Vector4>, float, IReadOnlyList<DenseMatrix<Complex64>>)>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution.BokehBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        /// <param name="components">
        /// The number of components to use to approximate the original 2D bokeh blur convolution kernel.
        /// </param>
        /// <param name="gamma">
        /// The gamma highlight factor to use to further process the image.
        /// </param>
        public BokehBlurProcessor(int radius = 32, int components = 2, float gamma = 3)
        {
            this.Radius = radius;
            this.kernelSize = (radius * 2) + 1;
            this.componentsCount = components;

            SixLabors.Guard.MustBeGreaterThanOrEqualTo(gamma, 1, nameof(gamma));
            this.Gamma = gamma;

            // Reuse the initialized values from the cache, if possible
            if (Cache.TryGetValue((radius, components), out (IReadOnlyList<Vector4>, float, IReadOnlyList<DenseMatrix<Complex64>>) info))
            {
                this.kernelParameters = info.Item1;
                this.kernelsScale = info.Item2;
                this.Kernels = info.Item3;
            }
            else
            {
                // Initialize the complex kernels and parameters with the current arguments
                (this.kernelParameters, this.kernelsScale) = this.GetParameters();
                this.Kernels = (
                    from parameters in this.kernelParameters
                    select this.CreateComplex1DKernel(parameters.X, parameters.Y)).ToArray();
                this.NormalizeKernels();

                // Store them in the cache for future use
                Cache.Add((radius, components), (this.kernelParameters, this.kernelsScale, this.Kernels));
            }
        }

        /// <summary>
        /// Gets the Radius
        /// </summary>
        public int Radius { get; }

        /// <summary>
        /// Gets the complex kernels to use to apply the blur for the current instance
        /// </summary>
        public IReadOnlyList<DenseMatrix<Complex64>> Kernels { get; }

        /// <summary>
        /// Gets the gamma highlight factor to use when applying the effect
        /// </summary>
        public float Gamma { get; }

        /// <summary>
        /// Gets the kernel scales to adjust the component values in each kernel
        /// </summary>
        private static IReadOnlyList<float> KernelScales { get; } = new[] { 1.4f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f };

        /// <summary>
        /// Gets the available bokeh blur kernel parameters
        /// </summary>
        private static IReadOnlyList<float[,]> KernelComponents { get; } = new[]
        {
            // 1 component
            new[,] { { 0.862325f, 1.624835f, 0.767583f, 1.862321f } },

            // 2 components
            new[,]
            {
                { 0.886528f, 5.268909f, 0.411259f, -0.548794f },
                { 1.960518f, 1.558213f, 0.513282f, 4.56111f }
            },

            // 3 components
            new[,]
            {
                { 2.17649f, 5.043495f, 1.621035f, -2.105439f },
                { 1.019306f, 9.027613f, -0.28086f, -0.162882f },
                { 2.81511f, 1.597273f, -0.366471f, 10.300301f }
            },

            // 4 components
            new[,]
            {
                { 4.338459f, 1.553635f, -5.767909f, 46.164397f },
                { 3.839993f, 4.693183f, 9.795391f, -15.227561f },
                { 2.791880f, 8.178137f, -3.048324f, 0.302959f },
                { 1.342190f, 12.328289f, 0.010001f, 0.244650f }
            },

            // 5 components
            new[,]
            {
                { 4.892608f, 1.685979f, -22.356787f, 85.91246f },
                { 4.71187f, 4.998496f, 35.918936f, -28.875618f },
                { 4.052795f, 8.244168f, -13.212253f, -1.578428f },
                { 2.929212f, 11.900859f, 0.507991f, 1.816328f },
                { 1.512961f, 16.116382f, 0.138051f, -0.01f }
            },

            // 6 components
            new[,]
            {
                { 5.143778f, 2.079813f, -82.326596f, 111.231024f },
                { 5.612426f, 6.153387f, 113.878661f, 58.004879f },
                { 5.982921f, 9.802895f, 39.479083f, -162.028887f },
                { 6.505167f, 11.059237f, -71.286026f, 95.027069f },
                { 3.869579f, 14.81052f, 1.405746f, -3.704914f },
                { 2.201904f, 19.032909f, -0.152784f, -0.107988f }
            }
        };

        /// <summary>
        /// Gets the kernel parameters and scaling factor for the current count value in the current instance
        /// </summary>
        private (IReadOnlyList<Vector4> Parameters, float Scale) GetParameters()
        {
            // Prepare the kernel components
            int index = Math.Max(0, Math.Min(this.componentsCount - 1, KernelComponents.Count));
            float[,] parameters = KernelComponents[index];
            var mapping = new Vector4[parameters.GetLength(0)];
            for (int i = 0; i < parameters.GetLength(0); i++)
            {
                mapping[i] = new Vector4(
                    parameters[i, 0],
                    parameters[i, 1],
                    parameters[i, 2],
                    parameters[i, 3]);
            }

            // Return the components and the adjustment scale
            return (mapping, KernelScales[index]);
        }

        /// <summary>
        /// Creates a complex 1D kernel with the specified parameters
        /// </summary>
        /// <param name="a">The exponential parameter for each complex component</param>
        /// <param name="b">The angle component for each complex component</param>
        private DenseMatrix<Complex64> CreateComplex1DKernel(float a, float b)
        {
            // Precompute the range values
            float[] ax = Enumerable.Range(-this.Radius, (this.Radius * 2) + 1).Select(
                i =>
                    {
                        float value = i * this.kernelsScale * (1f / this.Radius);
                        return value * value;
                    }).ToArray();

            // Compute the complex kernels
            var kernel = new DenseMatrix<Complex64>(1, this.kernelSize);
            for (int i = 0; i < this.kernelSize; i++)
            {
                float
                    real = (float)(Math.Exp(-a * ax[i]) * Math.Cos(b * ax[i])),
                    imaginary = (float)(Math.Exp(-a * ax[i]) * Math.Sin(b * ax[i]));
                kernel[i, 0] = new Complex64(real, imaginary);
            }

            return kernel;
        }

        /// <summary>
        /// Normalizes the kernels with respect to A * real + B * imaginary
        /// </summary>
        private void NormalizeKernels()
        {
            // Calculate the complex weighted sum
            double total = 0;
            foreach ((DenseMatrix<Complex64> kernel, Vector4 param) in this.Kernels.Zip(this.kernelParameters, (k, p) => (k, p)))
            {
                for (int i = 0; i < kernel.Count; i++)
                {
                    for (int j = 0; j < kernel.Count; j++)
                    {
                        total +=
                            (param.Z * ((kernel[i, 0].Real * kernel[j, 0].Real) - (kernel[i, 0].Imaginary * kernel[j, 0].Imaginary))) +
                            (param.W * ((kernel[i, 0].Real * kernel[j, 0].Imaginary) + (kernel[i, 0].Imaginary * kernel[j, 0].Real)));
                    }
                }
            }

            // Normalize the kernels
            float scalar = (float)(1f / Math.Sqrt(total));
            foreach (DenseMatrix<Complex64> kernel in this.Kernels)
            {
                for (int i = 0; i < kernel.Count; i++)
                {
                    kernel[i, 0] *= scalar;
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            // Create a 0-filled buffer to use to store the result of the component convolutions
            using (Buffer2D<Vector4> processing = configuration.MemoryAllocator.Allocate2D<Vector4>(source.Size()))
            {
                // Apply the complex 1D convolutions
                Span<Vector4> processingSpan = processing.Span;
                this.OnFrameApplyCore(source, processingSpan, sourceRectangle, configuration);

                // Copy the processed buffer back to the source image
                Span<TPixel> sourceSpan = source.GetPixelSpan();
                for (int i = 0; i < sourceSpan.Length; i++)
                {
                    TPixel pixel = default;
                    pixel.FromVector4(processingSpan[i]);
                    sourceSpan[i] = pixel;
                }
            }
        }

        /// <summary>
        /// Applies the actual bokeh blur effect on the target image frame
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="processingSpan">The target <see cref="Span{T}"/> with the buffer to write to.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        private void OnFrameApplyCore(ImageFrame<TPixel> source, Span<Vector4> processingSpan, Rectangle sourceRectangle, Configuration configuration)
        {
            // Perform two 1D convolutions for each component in the current instance
            foreach ((DenseMatrix<Complex64> kernel, Vector4 parameters) in this.Kernels.Zip(this.kernelParameters, (k, p) => (k, p)))
            {
                using (Buffer2D<ComplexVector4> partialValues = configuration.MemoryAllocator.Allocate2D<ComplexVector4>(source.Size()))
                {
                    // Compute the resulting complex buffer for the current component
                    using (Buffer2D<ComplexVector4> firstPassValues = configuration.MemoryAllocator.Allocate2D<ComplexVector4>(source.Size()))
                    {
                        var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
                        this.ApplyConvolution(firstPassValues, source.PixelBuffer, interest, kernel, configuration);
                        this.ApplyConvolution(partialValues, firstPassValues, interest, kernel.Reshape(kernel.Count, 1), configuration);
                    }

                    // Add the results of the convolution with the current kernel
                    Span<ComplexVector4> partialSpan = partialValues.Span;
                    for (int i = 0; i < processingSpan.Length; i++)
                    {
                        processingSpan[i] += partialSpan[i].WeightedSum(parameters.Z, parameters.W);
                    }
                }
            }
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="targetValues">The target <see cref="ComplexVector4"/> values to use to store the results.</param>
        /// <param name="sourcePixels">The source pixels. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="kernel">The kernel operator.</param>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        private void ApplyConvolution(
            Buffer2D<ComplexVector4> targetValues,
            Buffer2D<TPixel> sourcePixels,
            Rectangle sourceRectangle,
            in DenseMatrix<Complex64> kernel,
            Configuration configuration)
        {
            DenseMatrix<Complex64> matrix = kernel;
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);
            int width = workingRectangle.Width;

            ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                workingRectangle,
                configuration,
                (rows, vectorBuffer) =>
                {
                    for (int y = rows.Min; y < rows.Max; y++)
                    {
                        Span<ComplexVector4> targetRowSpan = targetValues.GetRowSpan(y).Slice(startX);

                        for (int x = 0; x < width; x++)
                        {
                            DenseMatrixUtils.Convolve(in matrix, sourcePixels, targetRowSpan, y, x, maxY, maxX, startX);
                        }
                    }
                });
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="Buffer2D{T}"/> buffer at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="targetValues">The target <see cref="ComplexVector4"/> values to use to store the results.</param>
        /// <param name="sourceValues">The source complex values. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="kernel">The kernel operator.</param>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        private void ApplyConvolution(
            Buffer2D<ComplexVector4> targetValues,
            Buffer2D<ComplexVector4> sourceValues,
            Rectangle sourceRectangle,
            in DenseMatrix<Complex64> kernel,
            Configuration configuration)
        {
            DenseMatrix<Complex64> matrix = kernel;
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);
            int width = workingRectangle.Width;

            ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                workingRectangle,
                configuration,
                (rows, vectorBuffer) =>
                {
                    for (int y = rows.Min; y < rows.Max; y++)
                    {
                        Span<ComplexVector4> targetRowSpan = targetValues.GetRowSpan(y).Slice(startX);

                        for (int x = 0; x < width; x++)
                        {
                            DenseMatrixUtils.Convolve(in matrix, sourceValues, targetRowSpan, y, x, maxY, maxX, startX);
                        }
                    }
                });
        }
    }
}
