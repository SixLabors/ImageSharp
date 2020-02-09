// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Convolution.Parameters;

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
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="BoxBlurProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public BokehBlurProcessor(Configuration configuration, BokehBlurProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.radius = definition.Radius;
            this.kernelSize = (this.radius * 2) + 1;
            this.componentsCount = definition.Components;
            this.gamma = definition.Gamma;

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
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            // Preliminary gamma highlight pass
            var gammaOperation = new ApplyGammaExposureRowIntervalOperation(this.SourceRectangle, source.PixelBuffer, this.Configuration, this.gamma);
            ParallelRowIterator.IterateRows<ApplyGammaExposureRowIntervalOperation, Vector4>(
                this.Configuration,
                this.SourceRectangle,
                in gammaOperation);

            // Create a 0-filled buffer to use to store the result of the component convolutions
            using Buffer2D<Vector4> processingBuffer = this.Configuration.MemoryAllocator.Allocate2D<Vector4>(source.Size(), AllocationOptions.Clean);

            // Perform the 1D convolutions on all the kernel components and accumulate the results
            this.OnFrameApplyCore(source, this.SourceRectangle, this.Configuration, processingBuffer);

            float inverseGamma = 1 / this.gamma;

            // Apply the inverse gamma exposure pass, and write the final pixel data
            var operation = new ApplyInverseGammaExposureRowIntervalOperation(this.SourceRectangle, source.PixelBuffer, processingBuffer, this.Configuration, inverseGamma);
            ParallelRowIterator.IterateRows(
                this.Configuration,
                this.SourceRectangle,
                in operation);
        }

        /// <summary>
        /// Computes and aggregates the convolution for each complex kernel component in the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="processingBuffer">The buffer with the raw pixel data to use to aggregate the results of each convolution.</param>
        private void OnFrameApplyCore(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration,
            Buffer2D<Vector4> processingBuffer)
        {
            // Allocate the buffer with the intermediate convolution results
            using Buffer2D<ComplexVector4> firstPassBuffer = this.Configuration.MemoryAllocator.Allocate2D<ComplexVector4>(source.Size());

            // Perform two 1D convolutions for each component in the current instance
            ref Complex64[] baseRef = ref MemoryMarshal.GetReference(this.kernels.AsSpan());
            ref Vector4 paramsRef = ref MemoryMarshal.GetReference(this.kernelParameters.AsSpan());
            for (int i = 0; i < this.kernels.Length; i++)
            {
                // Compute the resulting complex buffer for the current component
                Complex64[] kernel = Unsafe.Add(ref baseRef, i);
                Vector4 parameters = Unsafe.Add(ref paramsRef, i);

                // Compute the vertical 1D convolution
                var verticalOperation = new ApplyVerticalConvolutionRowIntervalOperation(ref sourceRectangle, firstPassBuffer, source.PixelBuffer, kernel);
                ParallelRowIterator.IterateRows(
                    configuration,
                    sourceRectangle,
                    in verticalOperation);

                // Compute the horizontal 1D convolutions and accumulate the partial results on the target buffer
                var horizontalOperation = new ApplyHorizontalConvolutionRowIntervalOperation(ref sourceRectangle, processingBuffer, firstPassBuffer, kernel, parameters.Z, parameters.W);
                ParallelRowIterator.IterateRows(
                    configuration,
                    sourceRectangle,
                    in horizontalOperation);
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the vertical convolution logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        private readonly struct ApplyVerticalConvolutionRowIntervalOperation : IRowIntervalOperation
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<ComplexVector4> targetValues;
            private readonly Buffer2D<TPixel> sourcePixels;
            private readonly Complex64[] kernel;
            private readonly int maxY;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ApplyVerticalConvolutionRowIntervalOperation(
                ref Rectangle bounds,
                Buffer2D<ComplexVector4> targetValues,
                Buffer2D<TPixel> sourcePixels,
                Complex64[] kernel)
            {
                this.bounds = bounds;
                this.maxY = this.bounds.Bottom - 1;
                this.maxX = this.bounds.Right - 1;
                this.targetValues = targetValues;
                this.sourcePixels = sourcePixels;
                this.kernel = kernel;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<ComplexVector4> targetRowSpan = this.targetValues.GetRowSpan(y).Slice(this.bounds.X);

                    for (int x = 0; x < this.bounds.Width; x++)
                    {
                        Buffer2DUtils.Convolve4(this.kernel, this.sourcePixels, targetRowSpan, y, x, this.bounds.Y, this.maxY, this.bounds.X, this.maxX);
                    }
                }
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the horizontal convolution logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        private readonly struct ApplyHorizontalConvolutionRowIntervalOperation : IRowIntervalOperation
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<Vector4> targetValues;
            private readonly Buffer2D<ComplexVector4> sourceValues;
            private readonly Complex64[] kernel;
            private readonly float z;
            private readonly float w;
            private readonly int maxY;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ApplyHorizontalConvolutionRowIntervalOperation(
                ref Rectangle bounds,
                Buffer2D<Vector4> targetValues,
                Buffer2D<ComplexVector4> sourceValues,
                Complex64[] kernel,
                float z,
                float w)
            {
                this.bounds = bounds;
                this.maxY = this.bounds.Bottom - 1;
                this.maxX = this.bounds.Right - 1;
                this.targetValues = targetValues;
                this.sourceValues = sourceValues;
                this.kernel = kernel;
                this.z = z;
                this.w = w;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<Vector4> targetRowSpan = this.targetValues.GetRowSpan(y).Slice(this.bounds.X);

                    for (int x = 0; x < this.bounds.Width; x++)
                    {
                        Buffer2DUtils.Convolve4AndAccumulatePartials(this.kernel, this.sourceValues, targetRowSpan, y, x, this.bounds.Y, this.maxY, this.bounds.X, this.maxX, this.z, this.w);
                    }
                }
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the gamma exposure logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        private readonly struct ApplyGammaExposureRowIntervalOperation : IRowIntervalOperation<Vector4>
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Configuration configuration;
            private readonly float gamma;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ApplyGammaExposureRowIntervalOperation(
                Rectangle bounds,
                Buffer2D<TPixel> targetPixels,
                Configuration configuration,
                float gamma)
            {
                this.bounds = bounds;
                this.targetPixels = targetPixels;
                this.configuration = configuration;
                this.gamma = gamma;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows, Span<Vector4> span)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> targetRowSpan = this.targetPixels.GetRowSpan(y).Slice(this.bounds.X);
                    PixelOperations<TPixel>.Instance.ToVector4(this.configuration, targetRowSpan.Slice(0, span.Length), span, PixelConversionModifiers.Premultiply);
                    ref Vector4 baseRef = ref MemoryMarshal.GetReference(span);

                    for (int x = 0; x < this.bounds.Width; x++)
                    {
                        ref Vector4 v = ref Unsafe.Add(ref baseRef, x);
                        v.X = MathF.Pow(v.X, this.gamma);
                        v.Y = MathF.Pow(v.Y, this.gamma);
                        v.Z = MathF.Pow(v.Z, this.gamma);
                    }

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, targetRowSpan);
                }
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the inverse gamma exposure logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        private readonly struct ApplyInverseGammaExposureRowIntervalOperation : IRowIntervalOperation
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Buffer2D<Vector4> sourceValues;
            private readonly Configuration configuration;
            private readonly float inverseGamma;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ApplyInverseGammaExposureRowIntervalOperation(
                Rectangle bounds,
                Buffer2D<TPixel> targetPixels,
                Buffer2D<Vector4> sourceValues,
                Configuration configuration,
                float inverseGamma)
            {
                this.bounds = bounds;
                this.targetPixels = targetPixels;
                this.sourceValues = sourceValues;
                this.configuration = configuration;
                this.inverseGamma = inverseGamma;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                Vector4 low = Vector4.Zero;
                var high = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> targetPixelSpan = this.targetPixels.GetRowSpan(y).Slice(this.bounds.X);
                    Span<Vector4> sourceRowSpan = this.sourceValues.GetRowSpan(y).Slice(this.bounds.X);
                    ref Vector4 sourceRef = ref MemoryMarshal.GetReference(sourceRowSpan);

                    for (int x = 0; x < this.bounds.Width; x++)
                    {
                        ref Vector4 v = ref Unsafe.Add(ref sourceRef, x);
                        var clamp = Vector4.Clamp(v, low, high);
                        v.X = MathF.Pow(clamp.X, this.inverseGamma);
                        v.Y = MathF.Pow(clamp.Y, this.inverseGamma);
                        v.Z = MathF.Pow(clamp.Z, this.inverseGamma);
                    }

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, sourceRowSpan.Slice(0, this.bounds.Width), targetPixelSpan, PixelConversionModifiers.Premultiply);
                }
            }
        }
    }
}
