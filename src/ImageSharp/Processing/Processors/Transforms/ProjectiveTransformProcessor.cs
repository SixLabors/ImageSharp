// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines a projective transformation applicable to an <see cref="Image"/>.
    /// </summary>
    public sealed class ProjectiveTransformProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformProcessor"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix.</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        /// <param name="targetDimensions">The target dimensions.</param>
        public ProjectiveTransformProcessor(Matrix4x4 matrix, IResampler sampler, Size targetDimensions)
        {
            Guard.NotNull(sampler, nameof(sampler));
            this.Sampler = sampler;
            this.TransformMatrix = matrix;
            this.TargetDimensions = targetDimensions;
        }

        /// <summary>
        /// Gets the sampler to perform interpolation of the transform operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets the matrix used to supply the projective transform.
        /// </summary>
        public Matrix4x4 TransformMatrix { get; }

        /// <summary>
        /// Gets the target dimensions to constrain the transformed image to.
        /// </summary>
        public Size TargetDimensions { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new ProjectiveTransformProcessor<TPixel>(this);
        }
    }
}