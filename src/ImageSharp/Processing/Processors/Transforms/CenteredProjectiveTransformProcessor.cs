// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// A base class that provides methods to allow the automatic centering of non-affine transforms
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class CenteredProjectiveTransformProcessor<TPixel> : ProjectiveTransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CenteredProjectiveTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        protected CenteredProjectiveTransformProcessor(Matrix4x4 matrix, IResampler sampler)
            : base(matrix, sampler)
        {
        }

        /// <inheritdoc/>
        protected override Matrix4x4 GetProcessingMatrix(Rectangle sourceRectangle, Rectangle destinationRectangle)
        {
            var translationToTargetCenter = Matrix4x4.CreateTranslation(-destinationRectangle.Width * .5F, -destinationRectangle.Height * .5F, 0);
            var translateToSourceCenter = Matrix4x4.CreateTranslation(sourceRectangle.Width * .5F, sourceRectangle.Height * .5F, 0);
            return translationToTargetCenter * this.TransformMatrix * translateToSourceCenter;
        }

        /// <inheritdoc/>
        protected override Rectangle GetTransformedBoundingRectangle(Rectangle sourceRectangle, Matrix4x4 matrix)
        {
            return Matrix4x4.Invert(this.TransformMatrix, out Matrix4x4 sizeMatrix)
                ? TransformHelpers.GetTransformedBoundingRectangle(sourceRectangle, sizeMatrix)
                : sourceRectangle;
        }
    }
}