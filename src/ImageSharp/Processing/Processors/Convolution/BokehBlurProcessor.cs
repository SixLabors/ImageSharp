// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies bokeh blur processing to the image.
    /// </summary>
    public sealed class BokehBlurProcessor : IImageProcessor
    {
        /// <summary>
        /// The default radius used by the parameterless constructor.
        /// </summary>
        public const int DefaultRadius = 32;

        /// <summary>
        /// The default component count used by the parameterless constructor.
        /// </summary>
        public const int DefaultComponents = 2;

        /// <summary>
        /// The default gamma used by the parameterless constructor.
        /// </summary>
        public const float DefaultGamma = 3F;

        /// <summary>
        /// Initializes a new instance of the <see cref="BokehBlurProcessor"/> class.
        /// </summary>
        public BokehBlurProcessor()
        {
            this.Radius = DefaultRadius;
            this.Components = DefaultComponents;
            this.Gamma = DefaultGamma;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BokehBlurProcessor"/> class.
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
        public BokehBlurProcessor(int radius, int components, float gamma)
        {
            Guard.MustBeGreaterThan(radius, 0, nameof(radius));
            Guard.MustBeBetweenOrEqualTo(components, 1, 6, nameof(components));
            Guard.MustBeGreaterThanOrEqualTo(gamma, 1, nameof(gamma));

            this.Radius = radius;
            this.Components = components;
            this.Gamma = gamma;
        }

        /// <summary>
        /// Gets the radius.
        /// </summary>
        public int Radius { get; }

        /// <summary>
        /// Gets the number of components.
        /// </summary>
        public int Components { get; }

        /// <summary>
        /// Gets the gamma highlight factor to use when applying the effect.
        /// </summary>
        public float Gamma { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new BokehBlurProcessor<TPixel>(configuration, this, source, sourceRectangle);

        /// <summary>
        /// A <see langword="struct"/> implementing the horizontal convolution logic for <see cref="BokehBlurProcessor{T}"/>.
        /// </summary>
        /// <remarks>
        /// This type is located in the non-generic <see cref="BokehBlurProcessor"/> class and not in <see cref="BokehBlurProcessor{TPixel}"/>, where
        /// it is actually used, because it does not use any generic parameters internally. Defining in a non-generic class means that there will only
        /// ever be a single instantiation of this type for the JIT/AOT compilers to process, instead of having duplicate versions for each pixel type.
        /// </remarks>
        internal readonly struct SecondPassConvolutionRowOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<Vector4> targetValues;
            private readonly Buffer2D<ComplexVector4> sourceValues;
            private readonly KernelSamplingMap map;
            private readonly Complex64[] kernel;
            private readonly float z;
            private readonly float w;

            [MethodImpl(InliningOptions.ShortMethod)]
            public SecondPassConvolutionRowOperation(
                Rectangle bounds,
                Buffer2D<Vector4> targetValues,
                Buffer2D<ComplexVector4> sourceValues,
                KernelSamplingMap map,
                Complex64[] kernel,
                float z,
                float w)
            {
                this.bounds = bounds;
                this.targetValues = targetValues;
                this.sourceValues = sourceValues;
                this.map = map;
                this.kernel = kernel;
                this.z = z;
                this.w = w;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                int boundsX = this.bounds.X;
                int boundsWidth = this.bounds.Width;
                Span<Vector4> targetBuffer = this.targetValues.GetRowSpan(y);

                var state = new ConvolutionState<Complex64>(this.kernel, this.kernel.Length, 1, this.map);
                ref int sampleRowBase = ref state.GetSampleRow(y - this.bounds.Y);

                // The target buffer is zeroed initially and then it accumulates the results
                // of each partial convolution, so we don't have to clear it here as well.
                ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);

                ReadOnlyKernel<Complex64> kernel = state.Kernel;

                for (int kY = 0; kY < kernel.Rows; kY++)
                {
                    // Get the precalculated source sample row for this kernel row and copy to our buffer.
                    int sampleY = Unsafe.Add(ref sampleRowBase, kY);
                    Span<ComplexVector4> sourceRow = this.sourceValues.GetRowSpan(sampleY).Slice(boundsX, boundsWidth);
                    ref ComplexVector4 sourceBase = ref MemoryMarshal.GetReference(sourceRow);

                    for (int x = 0; x < boundsWidth; x++)
                    {
                        ref int sampleColumnBase = ref state.GetSampleColumn(x);
                        ref Vector4 target = ref Unsafe.Add(ref targetBase, x);
                        ComplexVector4 pixel4 = default;

                        for (int kX = 0; kX < kernel.Columns; kX++)
                        {
                            int sampleX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                            ComplexVector4 sample = Unsafe.Add(ref sourceBase, sampleX);

                            pixel4.Sum(kernel[kY, kX] * sample);
                        }

                        target += pixel4.WeightedSum(this.z, this.w);
                    }
                }
            }
        }
    }
}
