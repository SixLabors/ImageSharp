// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides methods that allow the skewing of images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class SkewProcessor<TPixel> : AffineProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Matrix3x2 transformMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkewProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the skew operation.</param>
        public SkewProcessor(IResampler sampler)
            : base(sampler)
        {
        }

        /// <summary>
        /// Gets or sets the angle of rotation along the x-axis in degrees.
        /// </summary>
        public float AngleX { get; set; }

        /// <summary>
        /// Gets or sets the angle of rotation along the y-axis in degrees.
        /// </summary>
        public float AngleY { get; set; }

        /// <inheritdoc/>
        protected override Matrix3x2 CreateProcessingMatrix(Rectangle rectangle)
        {
            if (this.transformMatrix == default(Matrix3x2))
            {
                this.transformMatrix = Matrix3x2Extensions.CreateSkewDegrees(-this.AngleX, -this.AngleY, PointF.Empty);
            }

            return this.transformMatrix;
        }
    }
}