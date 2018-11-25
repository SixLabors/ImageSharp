// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
        private readonly List<Matrix3x2> matrices = new List<Matrix3x2>();
        private readonly Rectangle sourceRectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineTransformBuilder"/> class.
        /// </summary>
        /// <param name="sourceSize">The source image size.</param>
        public AffineTransformBuilder(Size sourceSize)
            : this(new Rectangle(Point.Empty, sourceSize))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineTransformBuilder"/> class.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        public AffineTransformBuilder(Rectangle sourceRectangle)
        {
            Guard.MustBeGreaterThan(sourceRectangle.Width, 0, nameof(sourceRectangle));
            Guard.MustBeGreaterThan(sourceRectangle.Height, 0, nameof(sourceRectangle));

            this.sourceRectangle = sourceRectangle;
        }

        /// <summary>
        /// Gets the source image size.
        /// </summary>
        internal Size Size => this.sourceRectangle.Size;

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependRotationDegrees(float degrees)
            => this.PrependMatrix(TransformUtils.CreateRotationMatrixDegrees(degrees, this.Size));

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependRotationRadians(float radians)
            => this.PrependMatrix(TransformUtils.CreateRotationMatrixRadians(radians, this.Size));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendRotationDegrees(float degrees)
            => this.AppendRotationRadians(ImageMaths.ToRadian(degrees));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendRotationRadians(float radians)
            => this.AppendMatrix(TransformUtils.CreateRotationMatrixRadians(radians, this.Size));

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
            => this.PrependMatrix(TransformUtils.CreateSkewMatrixDegrees(degreesX, degreesY, this.Size));

        /// <summary>
        /// Appends a centered skew matrix from the give angles in degrees.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendSkewDegrees(float degreesX, float degreesY)
            => this.AppendMatrix(TransformUtils.CreateSkewMatrixDegrees(degreesX, degreesY, this.Size));

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
            this.matrices.Insert(0, matrix);
            return this;
        }

        /// <summary>
        /// Appends a raw matrix.
        /// </summary>
        /// <param name="matrix">The matrix to append.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendMatrix(Matrix3x2 matrix)
        {
            this.matrices.Add(matrix);
            return this;
        }

        /// <summary>
        /// Returns the combined matrix.
        /// </summary>
        /// <returns>The <see cref="Matrix3x2"/>.</returns>
        public Matrix3x2 BuildMatrix()
        {
            Matrix3x2 matrix = Matrix3x2.Identity;

            // Translate the origin matrix to cater for source rectangle offsets.
            if (!this.sourceRectangle.Equals(default))
            {
                matrix *= Matrix3x2.CreateTranslation(-this.sourceRectangle.Location);
            }

            foreach (Matrix3x2 m in this.matrices)
            {
                matrix *= m;
            }

            return matrix;
        }

        /// <summary>
        /// Removes all matrices from the builder.
        /// </summary>
        public void Clear() => this.matrices.Clear();
    }
}