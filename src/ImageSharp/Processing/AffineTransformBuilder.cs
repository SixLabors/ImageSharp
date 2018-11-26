// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A helper class for constructing <see cref="Matrix3x2"/> instances for use in affine transforms.
    /// </summary>
    public class AffineTransformBuilder
    {
        private readonly List<Func<Size, Matrix3x2>> matrixFactories = new List<Func<Size, Matrix3x2>>();

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependCenteredRotationRadians(float radians)
            => this.Prepend(size => TransformUtils.CreateRotationMatrixRadians(radians, size));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendCenteredRotationRadians(float radians)
            => this.Append(size => TransformUtils.CreateRotationMatrixRadians(radians, size));

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependCenteredRotationDegrees(float degrees)
            => this.PrependCenteredRotationRadians(ImageMaths.DegreesToRadians(degrees));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendCenteredRotationDegrees(float degrees)
            => this.AppendCenteredRotationRadians(ImageMaths.DegreesToRadians(degrees));

        /// <summary>
        /// Prepends a scale matrix from the given uniform scale.
        /// </summary>
        /// <param name="scale">The uniform scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependScale(float scale)
            => this.PrependMatrix(Matrix3x2.CreateScale(scale));

        /// <summary>
        /// Appends a scale matrix from the given uniform scale.
        /// </summary>
        /// <param name="scale">The uniform scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendScale(float scale)
            => this.AppendMatrix(Matrix3x2.CreateScale(scale));

        /// <summary>
        /// Prepends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependScale(Vector2 scales)
            => this.PrependMatrix(Matrix3x2.CreateScale(scales));

        /// <summary>
        /// Appends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendScale(Vector2 scales)
            => this.AppendMatrix(Matrix3x2.CreateScale(scales));

        /// <summary>
        /// Prepends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scale">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependScale(SizeF scale)
            => this.PrependScale((Vector2)scale);

        /// <summary>
        /// Appends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendScale(SizeF scales)
            => this.AppendScale((Vector2)scales);

        /// <summary>
        /// Prepends a centered skew matrix from the give angles in degrees.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependSkewDegrees(float degreesX, float degreesY)
            => this.Prepend(size => TransformUtils.CreateSkewMatrixDegrees(degreesX, degreesY, size));

        /// <summary>
        /// Appends a centered skew matrix from the give angles in degrees.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendSkewDegrees(float degreesX, float degreesY)
            => this.Append(size => TransformUtils.CreateSkewMatrixDegrees(degreesX, degreesY, size));

        /// <summary>
        /// Prepends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependTranslation(Vector2 position)
            => this.PrependMatrix(Matrix3x2.CreateTranslation(position));

        /// <summary>
        /// Appends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendTranslation(Vector2 position)
            => this.AppendMatrix(Matrix3x2.CreateTranslation(position));

        /// <summary>
        /// Prepends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependTranslation(PointF position)
            => this.PrependTranslation((Vector2)position);

        /// <summary>
        /// Appends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendTranslation(PointF position)
            => this.AppendTranslation((Vector2)position);

        /// <summary>
        /// Prepends a raw matrix.
        /// </summary>
        /// <param name="matrix">The matrix to prepend.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependMatrix(Matrix3x2 matrix)
        {
            this.matrixFactories.Insert(0, _ => matrix);
            return this;
        }

        /// <summary>
        /// Appends a raw matrix.
        /// </summary>
        /// <param name="matrix">The matrix to append.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendMatrix(Matrix3x2 matrix)
        {
            this.matrixFactories.Add(_ => matrix);
            return this;
        }

        /// <summary>
        /// Returns the combined matrix for a given source size.
        /// </summary>
        /// <param name="sourceSize">The source image size</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        public Matrix3x2 BuildMatrix(Size sourceSize) => this.BuildMatrix(new Rectangle(Point.Empty, sourceSize));

        /// <summary>
        /// Returns the combined matrix for a given source rectangle.
        /// </summary>
        /// <param name="sourceRectangle">The rectangle in the source image</param>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        public Matrix3x2 BuildMatrix(Rectangle sourceRectangle)
        {
            Guard.MustBeGreaterThan(sourceRectangle.Width, 0, nameof(sourceRectangle));
            Guard.MustBeGreaterThan(sourceRectangle.Height, 0, nameof(sourceRectangle));

            // Translate the origin matrix to cater for source rectangle offsets.
            var matrix = Matrix3x2.CreateTranslation(-sourceRectangle.Location);

            Size size = sourceRectangle.Size;

            foreach (Func<Size, Matrix3x2> factory in this.matrixFactories)
            {
                matrix *= factory(size);
            }

            return matrix;
        }

        private AffineTransformBuilder Prepend(Func<Size, Matrix3x2> factory)
        {
            this.matrixFactories.Insert(0, factory);
            return this;
        }

        private AffineTransformBuilder Append(Func<Size, Matrix3x2> factory)
        {
            this.matrixFactories.Add(factory);
            return this;
        }
    }
}