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
        private Rectangle rectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineTransformBuilder"/> class.
        /// </summary>
        /// <param name="sourceSize">The source image size.</param>
        public AffineTransformBuilder(Size sourceSize) => this.Size = sourceSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineTransformBuilder"/> class.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        public AffineTransformBuilder(Rectangle sourceRectangle)
            : this(sourceRectangle.Size)
            => this.rectangle = sourceRectangle;

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependRotateMatrixDegrees(float degrees)
        {
            this.matrices.Insert(0, TransformUtils.CreateRotationMatrixDegrees(degrees, this.Size));
            return this;
        }

        /// <summary>
        /// Gets the source image size.
        /// </summary>
        internal Size Size { get; }

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendRotateMatrixDegrees(float degrees)
        {
            this.matrices.Add(TransformUtils.CreateRotationMatrixDegrees(degrees, this.Size));
            return this;
        }

        /// <summary>
        /// Prepends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependScaleMatrix(SizeF scales)
        {
            this.matrices.Insert(0, Matrix3x2Extensions.CreateScale(scales));
            return this;
        }

        /// <summary>
        /// Appends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendScaleMatrix(SizeF scales)
        {
            this.matrices.Add(Matrix3x2Extensions.CreateScale(scales));
            return this;
        }

        /// <summary>
        /// Prepends a centered skew matrix from the give angles in degrees.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependSkewMatrixDegrees(float degreesX, float degreesY)
        {
            this.matrices.Insert(0, TransformUtils.CreateSkewMatrixDegrees(degreesX, degreesY, this.Size));
            return this;
        }

        /// <summary>
        /// Appends a centered skew matrix from the give angles in degrees.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendSkewMatrixDegrees(float degreesX, float degreesY)
        {
            this.matrices.Add(TransformUtils.CreateSkewMatrixDegrees(degreesX, degreesY, this.Size));
            return this;
        }

        /// <summary>
        /// Prepends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder PrependTranslationMatrix(PointF position)
        {
            this.matrices.Insert(0, Matrix3x2Extensions.CreateTranslation(position));
            return this;
        }

        /// <summary>
        /// Appends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public AffineTransformBuilder AppendTranslationMatrix(PointF position)
        {
            this.matrices.Add(Matrix3x2Extensions.CreateTranslation(position));
            return this;
        }

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
            if (!this.rectangle.Equals(default))
            {
                matrix *= Matrix3x2.CreateTranslation(-this.rectangle.Location);
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