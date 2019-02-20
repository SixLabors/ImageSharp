// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
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
        /// The kernel components to use for the current instance
        /// </summary>
        private readonly IReadOnlyList<IReadOnlyDictionary<char, float>> kernelParameters;

        /// <summary>
        /// The scaling factor for kernel values
        /// </summary>
        private readonly float kernelsScale;

        /// <summary>
        /// The complex kernels to use to apply the blur for the current instance
        /// </summary>
        private readonly IReadOnlyList<Complex64[]> complexKernels;

        /// <summary>
        /// Initializes a new instance of the <see cref="SixLabors.ImageSharp.Processing.Processors.Convolution.BokehBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        /// <param name="components">
        /// The number of components to use to approximate the original 2D bokeh blur convolution kernel.
        /// </param>
        public BokehBlurProcessor(int radius = 32, int components = 2)
        {
            this.Radius = radius;
            this.kernelSize = (radius * 2) + 1;
            this.componentsCount = components;

            (this.kernelParameters, this.kernelsScale) = this.GetParameters();
            this.complexKernels = (
                from component in this.kernelParameters
                select this.CreateComplex1DKernel(component['a'], component['b'])).ToArray();
            this.NormalizeKernels();
        }

        /// <summary>
        /// Gets the Radius
        /// </summary>
        public int Radius { get; }

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
        private (IReadOnlyList<IReadOnlyDictionary<char, float>> Parameters, float Scale) GetParameters()
        {
            // Prepare the kernel components
            int index = Math.Max(0, Math.Min(this.componentsCount - 1, KernelComponents.Count));
            float[,] parameters = KernelComponents[index];
            var mapping = new IReadOnlyDictionary<char, float>[parameters.GetLength(0)];
            for (int i = 0; i < parameters.GetLength(0); i++)
            {
                mapping[i] = new Dictionary<char, float>
                {
                    ['a'] = parameters[i, 0],
                    ['b'] = parameters[i, 1],
                    ['A'] = parameters[i, 2],
                    ['B'] = parameters[i, 3]
                };
            }

            // Return the components and the adjustment scale
            return (mapping, KernelScales[index]);
        }

        /// <summary>
        /// Creates a complex 1D kernel with the specified parameters
        /// </summary>
        /// <param name="a">The exponential parameter for each complex component</param>
        /// <param name="b">The angle component for each complex component</param>
        private Complex64[] CreateComplex1DKernel(float a, float b)
        {
            // Precompute the range values
            float[] ax = Enumerable.Range(-this.Radius, this.Radius + 1).Select(
                i =>
                    {
                        float value = i * this.kernelsScale * (1f / this.Radius);
                        return value * value;
                    }).ToArray();

            // Compute the complex kernels
            var kernel = new Complex64[this.kernelSize];
            for (int i = 0; i < this.kernelSize; i++)
            {
                float
                    real = (float)(Math.Exp(-a * ax[i]) * Math.Cos(b * ax[i])),
                    imaginary = (float)(Math.Exp(-a * ax[i]) * Math.Sin(b * ax[i]));
                kernel[i] = new Complex64(real, imaginary);
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
            foreach ((Complex64[] kernel, IReadOnlyDictionary<char, float> param) in this.complexKernels.Zip(this.kernelParameters, (k, p) => (k, p)))
            {
                for (int i = 0; i < kernel.Length; i++)
                {
                    for (int j = 0; j < kernel.Length; j++)
                    {
                        total +=
                            (param['A'] * ((kernel[i].Real * kernel[j].Real) - (kernel[i].Imaginary * kernel[j].Imaginary))) +
                            (param['B'] * ((kernel[i].Real * kernel[j].Imaginary) + (kernel[i].Imaginary * kernel[j].Real)));
                    }
                }
            }

            // Normalize the kernels
            float scalar = (float)(1f / Math.Sqrt(total));
            foreach (Complex64[] kernel in this.complexKernels)
            {
                for (int i = 0; i < kernel.Length; i++)
                {
                    kernel[i] = kernel[i] * scalar;
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration) => throw new NotImplementedException();
    }
}
