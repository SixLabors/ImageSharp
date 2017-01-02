// <copyright file="Matrix3x2Processor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Provides methods to transform an image using a <see cref="Matrix3x2"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public abstract class Matrix3x2Processor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Gets the rectangle designating the target canvas.
        /// </summary>
        protected Rectangle CanvasRectangle { get; private set; }

        /// <summary>
        /// Creates a new target canvas to contain the results of the matrix transform.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        /// <param name="processMatrix">The processing matrix.</param>
        protected void CreateNewCanvas(Rectangle sourceRectangle, Matrix3x2 processMatrix)
        {
            Matrix3x2 sizeMatrix;
            this.CanvasRectangle = Matrix3x2.Invert(processMatrix, out sizeMatrix)
                ? ImageMaths.GetBoundingRectangle(sourceRectangle, sizeMatrix)
                : sourceRectangle;
        }

        /// <summary>
        /// Gets a transform matrix adjusted to center upon the target image bounds.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="matrix">The transform matrix.</param>
        /// <returns>
        /// The <see cref="Matrix3x2"/>.
        /// </returns>
        protected Matrix3x2 GetCenteredMatrix(ImageBase<TColor> source, Matrix3x2 matrix)
        {
            Matrix3x2 translationToTargetCenter = Matrix3x2.CreateTranslation(-this.CanvasRectangle.Width * .5F, -this.CanvasRectangle.Height * .5F);
            Matrix3x2 translateToSourceCenter = Matrix3x2.CreateTranslation(source.Width * .5F, source.Height * .5F);
            return (translationToTargetCenter * matrix) * translateToSourceCenter;
        }
    }
}
