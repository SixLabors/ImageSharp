// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
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
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// The gamma highlight factor to use when applying the effect
        /// </summary>
        private readonly float gamma;

        /// <summary>
        /// The kernel parameters to use for the current instance (a: X, b: Y, A: Z, B: W)
        /// </summary>
        private readonly Vector4[] kernelParameters;

        /// <summary>
        /// The kernel components for the current instance
        /// </summary>
        private readonly Complex64[][] kernels;

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
            this.gamma = definition.Gamma;

            // Get the bokeh blur data
            BokehBlurKernelData data = BokehBlurKernelDataProvider.GetBokehBlurKernelData(
                definition.Radius,
                (definition.Radius * 2) + 1,
                definition.Components);

            this.kernelParameters = data.Parameters;
            this.kernels = data.Kernels;
        }

        /// <summary>
        /// Gets the complex kernels to use to apply the blur for the current instance
        /// </summary>
        public IReadOnlyList<Complex64[]> Kernels => this.kernels;

        /// <summary>
        /// Gets the kernel parameters used to compute the pixel values from each complex pixel
        /// </summary>
        public IReadOnlyList<Vector4> KernelParameters => this.kernelParameters;

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            // Preliminary gamma highlight pass
            var gammaOperation = new ApplyGammaExposureRowOperation(this.SourceRectangle, source.PixelBuffer, this.Configuration, this.gamma);
            ParallelRowIterator.IterateRows<ApplyGammaExposureRowOperation, Vector4>(
                this.Configuration,
                this.SourceRectangle,
                in gammaOperation);

            // Create a 0-filled buffer to use to store the result of the component convolutions
            using Buffer2D<Vector4> processingBuffer = this.Configuration.MemoryAllocator.Allocate2D<Vector4>(source.Size(), AllocationOptions.Clean);

            // Perform the 1D convolutions on all the kernel components and accumulate the results
            this.OnFrameApplyCore(source, this.SourceRectangle, this.Configuration, processingBuffer);

            float inverseGamma = 1 / this.gamma;

            // Apply the inverse gamma exposure pass, and write the final pixel data
            var operation = new ApplyInverseGammaExposureRowOperation(this.SourceRectangle, source.PixelBuffer, processingBuffer, this.Configuration, inverseGamma);
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
                var verticalOperation = new ApplyVerticalConvolutionRowOperation(sourceRectangle, firstPassBuffer, source.PixelBuffer, kernel);
                ParallelRowIterator.IterateRows(
                    configuration,
                    sourceRectangle,
                    in verticalOperation);

                // Compute the horizontal 1D convolutions and accumulate the partial results on the target buffer
                var horizontalOperation = new ApplyHorizontalConvolutionRowOperation(sourceRectangle, processingBuffer, firstPassBuffer, kernel, parameters.Z, parameters.W);
                ParallelRowIterator.IterateRows(
                    configuration,
                    sourceRectangle,
                    in horizontalOperation);
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the vertical convolution logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        private readonly struct ApplyVerticalConvolutionRowOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<ComplexVector4> targetValues;
            private readonly Buffer2D<TPixel> sourcePixels;
            private readonly Complex64[] kernel;
            private readonly int maxY;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ApplyVerticalConvolutionRowOperation(
                Rectangle bounds,
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
            public void Invoke(int y)
            {
                Span<ComplexVector4> targetRowSpan = this.targetValues.GetRowSpan(y).Slice(this.bounds.X);

                for (int x = 0; x < this.bounds.Width; x++)
                {
                    Buffer2DUtils.Convolve4(this.kernel, this.sourcePixels, targetRowSpan, y, x, this.bounds.Y, this.maxY, this.bounds.X, this.maxX);
                }
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the horizontal convolution logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        private readonly struct ApplyHorizontalConvolutionRowOperation : IRowOperation
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
            public ApplyHorizontalConvolutionRowOperation(
                Rectangle bounds,
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
            public void Invoke(int y)
            {
                Span<Vector4> targetRowSpan = this.targetValues.GetRowSpan(y).Slice(this.bounds.X);

                for (int x = 0; x < this.bounds.Width; x++)
                {
                    Buffer2DUtils.Convolve4AndAccumulatePartials(this.kernel, this.sourceValues, targetRowSpan, y, x, this.bounds.Y, this.maxY, this.bounds.X, this.maxX, this.z, this.w);
                }
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the gamma exposure logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        private readonly struct ApplyGammaExposureRowOperation : IRowOperation<Vector4>
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Configuration configuration;
            private readonly float gamma;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ApplyGammaExposureRowOperation(
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
            public void Invoke(int y, Span<Vector4> span)
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

        /// <summary>
        /// A <see langword="struct"/> implementing the inverse gamma exposure logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        private readonly struct ApplyInverseGammaExposureRowOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Buffer2D<Vector4> sourceValues;
            private readonly Configuration configuration;
            private readonly float inverseGamma;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ApplyInverseGammaExposureRowOperation(
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
            public void Invoke(int y)
            {
                Vector4 low = Vector4.Zero;
                var high = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

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
