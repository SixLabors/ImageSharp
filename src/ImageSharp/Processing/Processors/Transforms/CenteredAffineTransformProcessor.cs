﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// A base class that provides methods to allow the automatic centering of affine transforms
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class CenteredAffineTransformProcessor<TPixel> : AffineTransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CenteredAffineTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        protected CenteredAffineTransformProcessor(Matrix3x2 matrix, IResampler sampler)
            : base(matrix, sampler)
        {
        }

        /// <inheritdoc/>
        protected override Matrix3x2 GetProcessingMatrix(Rectangle sourceRectangle, Rectangle destinationRectangle)
        {
            return TransformHelpers.GetCenteredTransformMatrix(sourceRectangle, destinationRectangle, this.TransformMatrix);
        }

        /// <inheritdoc/>
        protected override Size GetTransformedDimensions(Size sourceDimensions, Matrix3x2 matrix)
        {
            var sourceRectangle = new Rectangle(0, 0, sourceDimensions.Width, sourceDimensions.Height);
            return TransformHelpers.GetTransformedBoundingRectangle(sourceRectangle, this.TransformMatrix).Size;
        }
    }
}