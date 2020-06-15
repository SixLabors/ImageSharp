// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines a projective transformation applicable to an <see cref="Image"/>.
    /// </summary>
    public sealed class ProjectiveTransformProcessor : CloningImageProcessor
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
            Guard.MustBeValueType(sampler, nameof(sampler));

            this.Sampler = sampler;
            this.TransformMatrix = matrix;
            this.DestinationSize = targetDimensions;
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
        /// Gets the destination size to constrain the transformed image to.
        /// </summary>
        public Size DestinationSize { get; }

        /// <inheritdoc />
        public override ICloningImageProcessor<TPixel> CreatePixelSpecificCloningProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new ProjectiveTransformProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
