// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution.Parameters
{
    /// <summary>
    /// Provides parameters to be used in the <see cref="BokehBlurProcessor{TPixel}"/>.
    /// </summary>
    internal static class BokehBlurKernelDataProvider
    {
        /// <summary>
        /// The mapping of initialized complex kernels and parameters, to speed up the initialization of new <see cref="BokehBlurProcessor{TPixel}"/> instances
        /// </summary>
        private static readonly ConcurrentDictionary<BokehBlurParameters, BokehBlurKernelData> Cache = new ConcurrentDictionary<BokehBlurParameters, BokehBlurKernelData>();

        /// <summary>
        /// Gets the kernel scales to adjust the component values in each kernel
        /// </summary>
        private static IReadOnlyList<float> KernelScales { get; } = new[] { 1.4f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f };

        /// <summary>
        /// Gets the available bokeh blur kernel parameters
        /// </summary>
        private static IReadOnlyList<Vector4[]> KernelComponents { get; } = new[]
        {
            // 1 component
            new[] { new Vector4(0.862325f, 1.624835f, 0.767583f, 1.862321f) },

            // 2 components
            new[]
            {
                new Vector4(0.886528f, 5.268909f, 0.411259f, -0.548794f),
                new Vector4(1.960518f, 1.558213f, 0.513282f, 4.56111f)
            },

            // 3 components
            new[]
            {
                new Vector4(2.17649f, 5.043495f, 1.621035f, -2.105439f),
                new Vector4(1.019306f, 9.027613f, -0.28086f, -0.162882f),
                new Vector4(2.81511f, 1.597273f, -0.366471f, 10.300301f)
            },

            // 4 components
            new[]
            {
                new Vector4(4.338459f, 1.553635f, -5.767909f, 46.164397f),
                new Vector4(3.839993f, 4.693183f, 9.795391f, -15.227561f),
                new Vector4(2.791880f, 8.178137f, -3.048324f, 0.302959f),
                new Vector4(1.342190f, 12.328289f, 0.010001f, 0.244650f)
            },

            // 5 components
            new[]
            {
                new Vector4(4.892608f, 1.685979f, -22.356787f, 85.91246f),
                new Vector4(4.71187f, 4.998496f, 35.918936f, -28.875618f),
                new Vector4(4.052795f, 8.244168f, -13.212253f, -1.578428f),
                new Vector4(2.929212f, 11.900859f, 0.507991f, 1.816328f),
                new Vector4(1.512961f, 16.116382f, 0.138051f, -0.01f)
            },

            // 6 components
            new[]
            {
                new Vector4(5.143778f, 2.079813f, -82.326596f, 111.231024f),
                new Vector4(5.612426f, 6.153387f, 113.878661f, 58.004879f),
                new Vector4(5.982921f, 9.802895f, 39.479083f, -162.028887f),
                new Vector4(6.505167f, 11.059237f, -71.286026f, 95.027069f),
                new Vector4(3.869579f, 14.81052f, 1.405746f, -3.704914f),
                new Vector4(2.201904f, 19.032909f, -0.152784f, -0.107988f)
            }
        };

        /// <summary>
        /// Gets the bokeh blur kernel data for the specified parameters.
        /// </summary>
        /// <param name="radius">The value representing the size of the area to sample.</param>
        /// <param name="kernelSize">The size of each kernel to compute.</param>
        /// <param name="componentsCount">The number of components to use to approximate the original 2D bokeh blur convolution kernel.</param>
        /// <returns>A <see cref="BokehBlurKernelData"/> instance with the kernel data for the current parameters.</returns>
        public static BokehBlurKernelData GetBokehBlurKernelData(
            int radius,
            int kernelSize,
            int componentsCount)
        {
            // Reuse the initialized values from the cache, if possible
            var parameters = new BokehBlurParameters(radius, componentsCount);
            if (!Cache.TryGetValue(parameters, out BokehBlurKernelData info))
            {
                // Initialize the complex kernels and parameters with the current arguments
                (Vector4[] kernelParameters, float kernelsScale) = GetParameters(componentsCount);
                Complex64[][] kernels = CreateComplexKernels(kernelParameters, radius, kernelSize, kernelsScale);
                NormalizeKernels(kernels, kernelParameters);

                // Store them in the cache for future use
                info = new BokehBlurKernelData(kernelParameters, kernels);
                Cache.TryAdd(parameters, info);
            }

            return info;
        }

        /// <summary>
        /// Gets the kernel parameters and scaling factor for the current count value in the current instance
        /// </summary>
        private static (Vector4[] Parameters, float Scale) GetParameters(int componentsCount)
        {
            // Prepare the kernel components
            int index = Math.Max(0, Math.Min(componentsCount - 1, KernelComponents.Count));

            return (KernelComponents[index], KernelScales[index]);
        }

        /// <summary>
        /// Creates the collection of complex 1D kernels with the specified parameters
        /// </summary>
        /// <param name="kernelParameters">The parameters to use to normalize the kernels</param>
        /// <param name="radius">The value representing the size of the area to sample.</param>
        /// <param name="kernelSize">The size of each kernel to compute.</param>
        /// <param name="kernelsScale">The scale factor for each kernel.</param>
        private static Complex64[][] CreateComplexKernels(
            Vector4[] kernelParameters,
            int radius,
            int kernelSize,
            float kernelsScale)
        {
            var kernels = new Complex64[kernelParameters.Length][];
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(kernelParameters.AsSpan());
            for (int i = 0; i < kernelParameters.Length; i++)
            {
                ref Vector4 paramsRef = ref Unsafe.Add(ref baseRef, i);
                kernels[i] = CreateComplex1DKernel(radius, kernelSize, kernelsScale, paramsRef.X, paramsRef.Y);
            }

            return kernels;
        }

        /// <summary>
        /// Creates a complex 1D kernel with the specified parameters
        /// </summary>
        /// <param name="radius">The value representing the size of the area to sample.</param>
        /// <param name="kernelSize">The size of each kernel to compute.</param>
        /// <param name="kernelsScale">The scale factor for each kernel.</param>
        /// <param name="a">The exponential parameter for each complex component</param>
        /// <param name="b">The angle component for each complex component</param>
        private static Complex64[] CreateComplex1DKernel(
            int radius,
            int kernelSize,
            float kernelsScale,
            float a,
            float b)
        {
            var kernel = new Complex64[kernelSize];
            ref Complex64 baseRef = ref MemoryMarshal.GetReference(kernel.AsSpan());
            int r = radius, n = -r;

            for (int i = 0; i < kernelSize; i++, n++)
            {
                // Incrementally compute the range values
                float value = n * kernelsScale * (1f / r);
                value *= value;

                // Fill in the complex kernel values
                Unsafe.Add(ref baseRef, i) = new Complex64(
                    MathF.Exp(-a * value) * MathF.Cos(b * value),
                    MathF.Exp(-a * value) * MathF.Sin(b * value));
            }

            return kernel;
        }

        /// <summary>
        /// Normalizes the kernels with respect to A * real + B * imaginary
        /// </summary>
        /// <param name="kernels">The current convolution kernels to normalize</param>
        /// <param name="kernelParameters">The parameters to use to normalize the kernels</param>
        private static void NormalizeKernels(Complex64[][] kernels, Vector4[] kernelParameters)
        {
            // Calculate the complex weighted sum
            float total = 0;
            Span<Complex64[]> kernelsSpan = kernels.AsSpan();
            ref Complex64[] baseKernelsRef = ref MemoryMarshal.GetReference(kernelsSpan);
            ref Vector4 baseParamsRef = ref MemoryMarshal.GetReference(kernelParameters.AsSpan());

            for (int i = 0; i < kernelParameters.Length; i++)
            {
                ref Complex64[] kernelRef = ref Unsafe.Add(ref baseKernelsRef, i);
                int length = kernelRef.Length;
                ref Complex64 valueRef = ref kernelRef[0];
                ref Vector4 paramsRef = ref Unsafe.Add(ref baseParamsRef, i);

                for (int j = 0; j < length; j++)
                {
                    for (int k = 0; k < length; k++)
                    {
                        ref Complex64 jRef = ref Unsafe.Add(ref valueRef, j);
                        ref Complex64 kRef = ref Unsafe.Add(ref valueRef, k);
                        total +=
                            (paramsRef.Z * ((jRef.Real * kRef.Real) - (jRef.Imaginary * kRef.Imaginary)))
                            + (paramsRef.W * ((jRef.Real * kRef.Imaginary) + (jRef.Imaginary * kRef.Real)));
                    }
                }
            }

            // Normalize the kernels
            float scalar = 1f / MathF.Sqrt(total);
            for (int i = 0; i < kernelsSpan.Length; i++)
            {
                ref Complex64[] kernelsRef = ref Unsafe.Add(ref baseKernelsRef, i);
                int length = kernelsRef.Length;
                ref Complex64 valueRef = ref kernelsRef[0];

                for (int j = 0; j < length; j++)
                {
                    Unsafe.Add(ref valueRef, j) *= scalar;
                }
            }
        }
    }
}
