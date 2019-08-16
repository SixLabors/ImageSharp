// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Convolution.Parameters;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies bokeh blur processing to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <remarks>This processor is based on the code from Mike Pound, see <a href="https://github.com/mikepound/convolve">github.com/mikepound/convolve</a>.</remarks>
    internal class BokehBlurProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The kernel radius.
        /// </summary>
        private readonly int radius;

        /// <summary>
        /// The gamma highlight factor to use when applying the effect
        /// </summary>
        private readonly float gamma;

        /// <summary>
        /// The execution mode to use when applying the effect
        /// </summary>
        private readonly BokehBlurExecutionMode executionMode;

        /// <summary>
        /// The maximum size of the kernel in either direction
        /// </summary>
        private readonly int kernelSize;

        /// <summary>
        /// The number of components to use when applying the bokeh blur
        /// </summary>
        private readonly int componentsCount;

        /// <summary>
        /// The kernel parameters to use for the current instance (a: X, b: Y, A: Z, B: W)
        /// </summary>
        private readonly Vector4[] kernelParameters;

        /// <summary>
        /// The kernel components for the current instance
        /// </summary>
        private readonly Complex64[][] kernels;

        /// <summary>
        /// The scaling factor for kernel values
        /// </summary>
        private readonly float kernelsScale;

        /// <summary>
        /// The mapping of initialized complex kernels and parameters, to speed up the initialization of new <see cref="BokehBlurProcessor{TPixel}"/> instances
        /// </summary>
        private static readonly ConcurrentDictionary<BokehBlurParameters, BokehBlurKernelData> Cache = new ConcurrentDictionary<BokehBlurParameters, BokehBlurKernelData>();

        /// <summary>
        /// Initializes a new instance of the <see cref="BokehBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="definition">The <see cref="BoxBlurProcessor"/> defining the processor parameters.</param>
        public BokehBlurProcessor(BokehBlurProcessor definition)
        {
            this.radius = definition.Radius;
            this.kernelSize = (this.radius * 2) + 1;
            this.componentsCount = definition.Components;
            this.gamma = definition.Gamma;
            this.executionMode = definition.ExecutionMode;

            // Reuse the initialized values from the cache, if possible
            var parameters = new BokehBlurParameters(this.radius, this.componentsCount);
            if (Cache.TryGetValue(parameters, out BokehBlurKernelData info))
            {
                this.kernelParameters = info.Parameters;
                this.kernelsScale = info.Scale;
                this.kernels = info.Kernels;
            }
            else
            {
                // Initialize the complex kernels and parameters with the current arguments
                (this.kernelParameters, this.kernelsScale) = this.GetParameters();
                this.kernels = this.CreateComplexKernels();
                this.NormalizeKernels();

                // Store them in the cache for future use
                Cache.TryAdd(parameters, new BokehBlurKernelData(this.kernelParameters, this.kernelsScale, this.kernels));
            }
        }

        /// <summary>
        /// Gets the complex kernels to use to apply the blur for the current instance
        /// </summary>
        public IReadOnlyList<Complex64[]> Kernels => this.kernels;

        /// <summary>
        /// Gets the kernel parameters used to compute the pixel values from each complex pixel
        /// </summary>
        public IReadOnlyList<Vector4> KernelParameters => this.kernelParameters;

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
        /// Gets the kernel parameters and scaling factor for the current count value in the current instance
        /// </summary>
        private (Vector4[] Parameters, float Scale) GetParameters()
        {
            // Prepare the kernel components
            int index = Math.Max(0, Math.Min(this.componentsCount - 1, KernelComponents.Count));
            return (KernelComponents[index], KernelScales[index]);
        }

        /// <summary>
        /// Creates the collection of complex 1D kernels with the specified parameters
        /// </summary>
        private Complex64[][] CreateComplexKernels()
        {
            var kernels = new Complex64[this.kernelParameters.Length][];
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(this.kernelParameters.AsSpan());
            for (int i = 0; i < this.kernelParameters.Length; i++)
            {
                ref Vector4 paramsRef = ref Unsafe.Add(ref baseRef, i);
                kernels[i] = this.CreateComplex1DKernel(paramsRef.X, paramsRef.Y);
            }

            return kernels;
        }

        /// <summary>
        /// Creates a complex 1D kernel with the specified parameters
        /// </summary>
        /// <param name="a">The exponential parameter for each complex component</param>
        /// <param name="b">The angle component for each complex component</param>
        private Complex64[] CreateComplex1DKernel(float a, float b)
        {
            var kernel = new Complex64[this.kernelSize];
            ref Complex64 baseRef = ref MemoryMarshal.GetReference(kernel.AsSpan());
            int r = this.radius, n = -r;

            for (int i = 0; i < this.kernelSize; i++, n++)
            {
                // Incrementally compute the range values
                float value = n * this.kernelsScale * (1f / r);
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
        private void NormalizeKernels()
        {
            // Calculate the complex weighted sum
            float total = 0;
            Span<Complex64[]> kernelsSpan = this.kernels.AsSpan();
            ref Complex64[] baseKernelsRef = ref MemoryMarshal.GetReference(kernelsSpan);
            ref Vector4 baseParamsRef = ref MemoryMarshal.GetReference(this.kernelParameters.AsSpan());

            for (int i = 0; i < this.kernelParameters.Length; i++)
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

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            // Preliminary gamma highlight pass
            this.ApplyGammaExposure(source.PixelBuffer, sourceRectangle, configuration);

            // Create a 0-filled buffer to use to store the result of the component convolutions
            using (Buffer2D<Vector4> processing = configuration.MemoryAllocator.Allocate2D<Vector4>(source.Size(), AllocationOptions.Clean))
            {
                if (this.executionMode == BokehBlurExecutionMode.PreferLowMemoryUsage)
                {
                    // Memory usage priority: allocate a shared buffer and execute the second convolution in sequential mode
                    using (Buffer2D<ComplexVector4> buffer = configuration.MemoryAllocator.Allocate2D<ComplexVector4>(source.Width, source.Height + this.radius))
                    using (Buffer2D<ComplexVector4> firstPassBuffer = buffer.Slice(this.radius, source.Height))
                    using (Buffer2D<ComplexVector4> secondPassBuffer = buffer.Slice(0, source.Height))
                    {
                        this.OnFrameApplyCore(source, sourceRectangle, configuration, processing, firstPassBuffer, secondPassBuffer);
                    }
                }
                else
                {
                    // Performance priority: allocate two independent buffers and execute both convolutions in parallel mode
                    using (Buffer2D<ComplexVector4> firstPassValues = configuration.MemoryAllocator.Allocate2D<ComplexVector4>(source.Size()))
                    using (Buffer2D<ComplexVector4> secondPassBuffer = configuration.MemoryAllocator.Allocate2D<ComplexVector4>(source.Size()))
                    {
                        this.OnFrameApplyCore(source, sourceRectangle, configuration, processing, firstPassValues, secondPassBuffer);
                    }
                }

                // Apply the inverse gamma exposure pass, and write the final pixel data
                this.ApplyInverseGammaExposure(source.PixelBuffer, processing, sourceRectangle, configuration);
            }
        }

        /// <summary>
        /// Computes and aggregates the convolution for each complex kernel component in the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="processingBuffer">The buffer with the raw pixel data to use to aggregate the results of each convolution.</param>
        /// <param name="firstPassBuffer">The complex buffer to use for the first 1D convolution pass for each kernel.</param>
        /// <param name="secondPassBuffer">The complex buffer to use for the second 1D convolution pass for each kernel.</param>
        private void OnFrameApplyCore(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration,
            Buffer2D<Vector4> processingBuffer,
            Buffer2D<ComplexVector4> firstPassBuffer,
            Buffer2D<ComplexVector4> secondPassBuffer)
        {
            // Perform two 1D convolutions for each component in the current instance
            ref Complex64[] baseRef = ref MemoryMarshal.GetReference(this.kernels.AsSpan());
            for (int i = 0; i < this.kernels.Length; i++)
            {
                // Compute the resulting complex buffer for the current component
                var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
                Complex64[] kernel = Unsafe.Add(ref baseRef, i);
                this.ApplyConvolution(firstPassBuffer, source.PixelBuffer, interest, kernel, configuration);
                this.ApplyConvolution(secondPassBuffer, firstPassBuffer, interest, kernel, configuration);

                // Add the results of the convolution with the current kernel
                Vector4 parameters = this.kernelParameters[i];
                this.SumProcessingPartials(processingBuffer, secondPassBuffer, sourceRectangle, configuration, parameters.Z, parameters.W);
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
        /// <param name="kernel">The 1D kernel.</param>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        private void ApplyConvolution(
            Buffer2D<ComplexVector4> targetValues,
            Buffer2D<TPixel> sourcePixels,
            Rectangle sourceRectangle,
            Complex64[] kernel,
            Configuration configuration)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);
            int width = workingRectangle.Width;

            ParallelHelper.IterateRows(
                workingRectangle,
                configuration,
                rows =>
                {
                    for (int y = rows.Min; y < rows.Max; y++)
                    {
                        Span<ComplexVector4> targetRowSpan = targetValues.GetRowSpan(y).Slice(startX);

                        for (int x = 0; x < width; x++)
                        {
                            Buffer2DUtils.Convolve4(kernel, sourcePixels, targetRowSpan, y, x, startY, maxY, startX, maxX);
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
        /// <param name="kernel">The 1D kernel.</param>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        private void ApplyConvolution(
            Buffer2D<ComplexVector4> targetValues,
            Buffer2D<ComplexVector4> sourceValues,
            Rectangle sourceRectangle,
            Complex64[] kernel,
            Configuration configuration)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);
            int width = workingRectangle.Width;

            if (this.executionMode == BokehBlurExecutionMode.PreferLowMemoryUsage)
            {
                configuration = configuration.Clone();
                configuration.MaxDegreeOfParallelism = 1;
            }

            ParallelHelper.IterateRows(
                workingRectangle,
                configuration,
                rows =>
                {
                    for (int y = rows.Min; y < rows.Max; y++)
                    {
                        Span<ComplexVector4> targetRowSpan = targetValues.GetRowSpan(y).Slice(startX);

                        for (int x = 0; x < width; x++)
                        {
                            Buffer2DUtils.Convolve4(kernel, sourceValues, targetRowSpan, y, x, startY, maxY, startX, maxX);
                        }
                    }
                });
        }

        /// <summary>
        /// Applies the gamma correction/highlight to the input pixel buffer.
        /// </summary>
        /// <param name="targetPixels">The target pixel buffer to adjust.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        private void ApplyGammaExposure(
            Buffer2D<TPixel> targetPixels,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);
            int width = workingRectangle.Width;
            float exp = this.gamma;

            ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                workingRectangle,
                configuration,
                (rows, vectorBuffer) =>
                    {
                        Span<Vector4> vectorSpan = vectorBuffer.Span;
                        int length = vectorSpan.Length;

                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> targetRowSpan = targetPixels.GetRowSpan(y).Slice(startX);
                            PixelOperations<TPixel>.Instance.ToVector4(configuration, targetRowSpan.Slice(0, length), vectorSpan, PixelConversionModifiers.Premultiply);
                            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectorSpan);

                            for (int x = 0; x < width; x++)
                            {
                                ref Vector4 v = ref Unsafe.Add(ref baseRef, x);
                                v.X = MathF.Pow(v.X, exp);
                                v.Y = MathF.Pow(v.Y, exp);
                                v.Z = MathF.Pow(v.Z, exp);
                            }

                            PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorSpan.Slice(0, length), targetRowSpan);
                        }
                    });
        }

        /// <summary>
        /// Applies the inverse gamma correction/highlight pass, and converts the input <see cref="Vector4"/> buffer into pixel values.
        /// </summary>
        /// <param name="targetPixels">The target pixels to apply the process to.</param>
        /// <param name="sourceValues">The source <see cref="Vector4"/> values. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        private void ApplyInverseGammaExposure(
            Buffer2D<TPixel> targetPixels,
            Buffer2D<Vector4> sourceValues,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);
            int width = workingRectangle.Width;
            float expGamma = 1 / this.gamma;

            ParallelHelper.IterateRows(
                workingRectangle,
                configuration,
                rows =>
                    {
                        Vector4 low = Vector4.Zero;
                        var high = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> targetPixelSpan = targetPixels.GetRowSpan(y).Slice(startX);
                            Span<Vector4> sourceRowSpan = sourceValues.GetRowSpan(y).Slice(startX);
                            ref Vector4 sourceRef = ref MemoryMarshal.GetReference(sourceRowSpan);

                            for (int x = 0; x < width; x++)
                            {
                                ref Vector4 v = ref Unsafe.Add(ref sourceRef, x);
                                var clamp = Vector4.Clamp(v, low, high);
                                v.X = MathF.Pow(clamp.X, expGamma);
                                v.Y = MathF.Pow(clamp.Y, expGamma);
                                v.Z = MathF.Pow(clamp.Z, expGamma);
                            }

                            PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, sourceRowSpan.Slice(0, width), targetPixelSpan, PixelConversionModifiers.Premultiply);
                        }
                    });
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="targetValues">The target <see cref="Buffer2D{T}"/> instance to use to store the results.</param>
        /// <param name="sourceValues">The source complex pixels. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        /// <param name="z">The weight factor for the real component of the complex pixel values.</param>
        /// <param name="w">The weight factor for the imaginary component of the complex pixel values.</param>
        private void SumProcessingPartials(
            Buffer2D<Vector4> targetValues,
            Buffer2D<ComplexVector4> sourceValues,
            Rectangle sourceRectangle,
            Configuration configuration,
            float z,
            float w)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);
            int width = workingRectangle.Width;

            ParallelHelper.IterateRows(
                workingRectangle,
                configuration,
                rows =>
                {
                    for (int y = rows.Min; y < rows.Max; y++)
                    {
                        Span<Vector4> targetRowSpan = targetValues.GetRowSpan(y).Slice(startX);
                        Span<ComplexVector4> sourceRowSpan = sourceValues.GetRowSpan(y).Slice(startX);
                        ref Vector4 baseTargetRef = ref MemoryMarshal.GetReference(targetRowSpan);
                        ref ComplexVector4 baseSourceRef = ref MemoryMarshal.GetReference(sourceRowSpan);

                        for (int x = 0; x < width; x++)
                        {
                            Unsafe.Add(ref baseTargetRef, x) += Unsafe.Add(ref baseSourceRef, x).WeightedSum(z, w);
                        }
                    }
                });
        }
    }
}
