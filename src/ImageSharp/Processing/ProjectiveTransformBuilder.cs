// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A helper class for constructing <see cref="Matrix4x4"/> instances for use in projective transforms.
    /// </summary>
    public class ProjectiveTransformBuilder
    {
        private readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
        private Rectangle sourceRectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformBuilder"/> class.
        /// </summary>
        /// <param name="sourceSize">The source image size.</param>
        public ProjectiveTransformBuilder(Size sourceSize)
            : this(new Rectangle(Point.Empty, sourceSize))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformBuilder"/> class.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        public ProjectiveTransformBuilder(Rectangle sourceRectangle)
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
        /// Prepends a matrix that performs a tapering projective transform.
        /// </summary>
        /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
        /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
        /// <param name="fraction">The amount to taper.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependTaperMatrix(TaperSide side, TaperCorner corner, float fraction)
            => this.PrependMatrix(TransformUtils.CreateTaperMatrix(this.Size, side, corner, fraction));

        /// <summary>
        /// Appends a matrix that performs a tapering projective transform.
        /// </summary>
        /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
        /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
        /// <param name="fraction">The amount to taper.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendTaperMatrix(TaperSide side, TaperCorner corner, float fraction)
            => this.AppendMatrix(TransformUtils.CreateTaperMatrix(this.Size, side, corner, fraction));

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependCenteredRotationRadians(float radians)
        {
            var m = new Matrix4x4(TransformUtils.CreateRotationMatrixRadians(radians, this.Size));
            return this.PrependMatrix(m);
        }

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendCenteredRotationRadians(float radians)
        {
            var m = new Matrix4x4(TransformUtils.CreateRotationMatrixRadians(radians, this.Size));
            return this.AppendMatrix(m);
        }

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependCenteredRotationDegrees(float degrees)
            => this.PrependCenteredRotationRadians(ImageMaths.DegreesToRadians(degrees));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendCenteredRotationDegrees(float degrees)
            => this.AppendCenteredRotationRadians(ImageMaths.DegreesToRadians(degrees));

        /// <summary>
        /// Prepends a scale matrix from the given uniform scale.
        /// </summary>
        /// <param name="scale">The uniform scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependScale(float scale)
            => this.PrependMatrix(Matrix4x4.CreateScale(scale));

        /// <summary>
        /// Appends a scale matrix from the given uniform scale.
        /// </summary>
        /// <param name="scale">The uniform scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendScale(float scale)
            => this.AppendMatrix(Matrix4x4.CreateScale(scale));

        /// <summary>
        /// Prepends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependScale(Vector2 scales)
            => this.PrependMatrix(Matrix4x4.CreateScale(new Vector3(scales, 1f)));

        /// <summary>
        /// Appends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendScale(Vector2 scales)
            => this.AppendMatrix(Matrix4x4.CreateScale(new Vector3(scales, 1f)));

        /// <summary>
        /// Prepends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scale">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependScale(SizeF scale)
            => this.PrependScale((Vector2)scale);

        /// <summary>
        /// Appends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendScale(SizeF scales)
            => this.AppendScale((Vector2)scales);

        /// <summary>
        /// Prepends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependTranslation(Vector2 position)
            => this.PrependMatrix(Matrix4x4.CreateTranslation(new Vector3(position, 0)));

        /// <summary>
        /// Appends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendTranslation(Vector2 position)
            => this.AppendMatrix(Matrix4x4.CreateTranslation(new Vector3(position, 0)));

        /// <summary>
        /// Prepends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependTranslation(PointF position)
            => this.PrependTranslation((Vector2)position);

        /// <summary>
        /// Appends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="AffineTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendTranslation(PointF position)
            => this.AppendTranslation((Vector2)position);

        /// <summary>
        /// Prepends a raw matrix.
        /// </summary>
        /// <param name="matrix">The matrix to prepend.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependMatrix(Matrix4x4 matrix)
        {
            this.matrices.Insert(0, matrix);
            return this;
        }

        /// <summary>
        /// Appends a raw matrix.
        /// </summary>
        /// <param name="matrix">The matrix to append.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendMatrix(Matrix4x4 matrix)
        {
            this.matrices.Add(matrix);
            return this;
        }

        /// <summary>
        /// Returns the combined matrix.
        /// </summary>
        /// <returns>The <see cref="Matrix4x4"/>.</returns>
        public Matrix4x4 BuildMatrix()
        {
            Matrix4x4 matrix = Matrix4x4.Identity;

            // Translate the origin matrix to cater for source rectangle offsets.
            if (!this.sourceRectangle.Equals(default))
            {
                matrix *= Matrix4x4.CreateTranslation(new Vector3(-this.sourceRectangle.Location, 0));
            }

            foreach (Matrix4x4 m in this.matrices)
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