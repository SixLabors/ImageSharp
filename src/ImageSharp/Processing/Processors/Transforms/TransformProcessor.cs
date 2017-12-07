// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides methods that allow the tranformation of images using various algorithms.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class TransformProcessor<TPixel> : AffineProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transformation matrix</param>
        public TransformProcessor(Matrix3x2 matrix)
            : this(matrix, KnownResamplers.NearestNeighbor)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transformation matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        public TransformProcessor(Matrix3x2 matrix, IResampler sampler)
            : base(sampler)
        {
            // Tansforms are inverted else the output is the opposite of the expected.
            Matrix3x2.Invert(matrix, out matrix);
            this.TransformMatrix = matrix;
        }

        /// <summary>
        /// Gets the transform matrix
        /// </summary>
        public Matrix3x2 TransformMatrix { get; }

        /// <inheritdoc />
        protected override Matrix3x2 GetTransformMatrix()
        {
            return this.TransformMatrix;
        }
    }
}