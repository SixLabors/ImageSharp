// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Defines a rotation applicable to an <see cref="Image"/>.
    /// </summary>
    public sealed class RotateProcessor : AffineTransformProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RotateProcessor"/> class.
        /// </summary>
        /// <param name="degrees">The angle of rotation in degrees.</param>
        /// <param name="sourceSize">The source image size</param>
        public RotateProcessor(float degrees, Size sourceSize)
            : this(degrees, KnownResamplers.Bicubic, sourceSize)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RotateProcessor"/> class.
        /// </summary>
        /// <param name="degrees">The angle of rotation in degrees.</param>
        /// <param name="sampler">The sampler to perform the rotating operation.</param>
        /// <param name="sourceSize">The source image size</param>
        public RotateProcessor(float degrees, IResampler sampler, Size sourceSize)
            : this(
                TransformUtils.CreateRotationMatrixDegrees(degrees, sourceSize),
                sampler,
                sourceSize)
            => this.Degrees = degrees;

        // Helper constructor
        private RotateProcessor(Matrix3x2 rotationMatrix, IResampler sampler, Size sourceSize)
            : base(rotationMatrix, sampler, TransformUtils.GetTransformedSize(sourceSize, rotationMatrix))
        {
        }

        /// <summary>
        /// Gets the angle of rotation in degrees.
        /// </summary>
        public float Degrees { get; }

        /// <inheritdoc />
        public override ICloningImageProcessor<TPixel> CreatePixelSpecificCloningProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new RotateProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
