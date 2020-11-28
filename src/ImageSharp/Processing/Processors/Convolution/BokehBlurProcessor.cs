// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
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
        internal readonly struct ApplyHorizontalConvolutionRowOperation : IRowOperation
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
    }
}
