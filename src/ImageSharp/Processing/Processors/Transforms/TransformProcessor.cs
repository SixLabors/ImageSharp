// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

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
            : base(matrix, sampler)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        /// <param name="rectangle">The rectangle to constrain the transformed image to.</param>
        public TransformProcessor(Matrix3x2 matrix, IResampler sampler, Rectangle rectangle)
            : base(matrix, sampler, rectangle)
        {
        }
    }
}