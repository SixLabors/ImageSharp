// <copyright file="Matrix3x2Processor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;

    /// <summary>
    /// Provides methods to transform an image using a <see cref="Matrix3x2"/>.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public abstract class Matrix3x2Processor<T, TP> : ImageSampler<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Creates a new target to contain the results of the matrix transform.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="sourceRectangle">The source rectangle.</param>
        /// <param name="processMatrix">The processing matrix.</param>
        protected static void CreateNewTarget(ImageBase<T, TP> target, Rectangle sourceRectangle, Matrix3x2 processMatrix)
        {
            Matrix3x2 sizeMatrix;
            if (Matrix3x2.Invert(processMatrix, out sizeMatrix))
            {
                Rectangle rectangle = ImageMaths.GetBoundingRectangle(sourceRectangle, sizeMatrix);
                target.SetPixels(rectangle.Width, rectangle.Height, new T[rectangle.Width * rectangle.Height]);
            }
        }

        /// <summary>
        /// Gets a transform matrix adjusted to center upon the target image bounds.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image.</param>
        /// <param name="matrix">The transform matrix.</param>
        /// <returns>
        /// The <see cref="Matrix3x2"/>.
        /// </returns>
        protected static Matrix3x2 GetCenteredMatrix(ImageBase<T, TP> target, ImageBase<T, TP> source, Matrix3x2 matrix)
        {
            Matrix3x2 translationToTargetCenter = Matrix3x2.CreateTranslation(-target.Width * .5F, -target.Height * .5F);
            Matrix3x2 translateToSourceCenter = Matrix3x2.CreateTranslation(source.Width * .5F, source.Height * .5F);
            return (translationToTargetCenter * matrix) * translateToSourceCenter;
        }
    }
}
