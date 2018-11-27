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
    /// A helper class for constructing <see cref="Matrix4x4"/> instances for use in projective transforms.
    /// </summary>
    public class ProjectiveTransformBuilder
    {
        private readonly List<Func<Size, Matrix4x4>> matrixFactories = new List<Func<Size, Matrix4x4>>();

        /// <summary>
        /// Prepends a matrix that performs a tapering projective transform.
        /// </summary>
        /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
        /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
        /// <param name="fraction">The amount to taper.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependTaper(TaperSide side, TaperCorner corner, float fraction)
            => this.Prepend(size => TransformUtils.CreateTaperMatrix(size, side, corner, fraction));

        /// <summary>
        /// Appends a matrix that performs a tapering projective transform.
        /// </summary>
        /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
        /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
        /// <param name="fraction">The amount to taper.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendTaper(TaperSide side, TaperCorner corner, float fraction)
            => this.Append(size => TransformUtils.CreateTaperMatrix(size, side, corner, fraction));

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependRotationDegrees(float degrees)
            => this.PrependRotationRadians(GeometryUtilities.DegreeToRadian(degrees));

        /// <summary>
        /// Prepends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependRotationRadians(float radians)
            => this.Prepend(size => new Matrix4x4(TransformUtils.CreateRotationMatrixRadians(radians, size)));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <param name="centerPoint">The rotation center.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        internal ProjectiveTransformBuilder PrependRotationRadians(float radians, Vector2 centerPoint)
            => this.PrependMatrix(Matrix4x4.CreateRotationZ(radians, new Vector3(centerPoint, 0)));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in degrees.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendRotationDegrees(float degrees)
            => this.AppendRotationRadians(GeometryUtilities.DegreeToRadian(degrees));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendRotationRadians(float radians)
            => this.Append(size => new Matrix4x4(TransformUtils.CreateRotationMatrixRadians(radians, size)));

        /// <summary>
        /// Appends a centered rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <param name="centerPoint">The rotation center.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        internal ProjectiveTransformBuilder AppendRotationRadians(float radians, Vector2 centerPoint)
            => this.AppendMatrix(Matrix4x4.CreateRotationZ(radians, new Vector3(centerPoint, 0)));

        /// <summary>
        /// Prepends a scale matrix from the given uniform scale.
        /// </summary>
        /// <param name="scale">The uniform scale.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependScale(float scale)
            => this.PrependMatrix(Matrix4x4.CreateScale(scale));

        /// <summary>
        /// Prepends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scale">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependScale(SizeF scale)
            => this.PrependScale((Vector2)scale);

        /// <summary>
        /// Prepends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependScale(Vector2 scales)
            => this.PrependMatrix(Matrix4x4.CreateScale(new Vector3(scales, 1F)));

        /// <summary>
        /// Appends a scale matrix from the given uniform scale.
        /// </summary>
        /// <param name="scale">The uniform scale.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendScale(float scale)
            => this.AppendMatrix(Matrix4x4.CreateScale(scale));

        /// <summary>
        /// Appends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendScale(SizeF scales)
            => this.AppendScale((Vector2)scales);

        /// <summary>
        /// Appends a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The horizontal and vertical scale.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendScale(Vector2 scales)
            => this.AppendMatrix(Matrix4x4.CreateScale(new Vector3(scales, 1F)));

        /// <summary>
        /// Prepends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependTranslation(PointF position)
            => this.PrependTranslation((Vector2)position);

        /// <summary>
        /// Prepends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependTranslation(Vector2 position)
            => this.PrependMatrix(Matrix4x4.CreateTranslation(new Vector3(position, 0)));

        /// <summary>
        /// Appends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendTranslation(PointF position)
            => this.AppendTranslation((Vector2)position);

        /// <summary>
        /// Appends a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendTranslation(Vector2 position)
            => this.AppendMatrix(Matrix4x4.CreateTranslation(new Vector3(position, 0)));

        /// <summary>
        /// Prepends a raw matrix.
        /// </summary>
        /// <param name="matrix">The matrix to prepend.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependMatrix(Matrix4x4 matrix) => this.Prepend(_ => matrix);

        /// <summary>
        /// Appends a raw matrix.
        /// </summary>
        /// <param name="matrix">The matrix to append.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendMatrix(Matrix4x4 matrix) => this.Append(_ => matrix);

        /// <summary>
        /// Returns the combined matrix for a given source size.
        /// </summary>
        /// <param name="sourceSize">The source image size.</param>
        /// <returns>The <see cref="Matrix4x4"/>.</returns>
        public Matrix4x4 BuildMatrix(Size sourceSize) => this.BuildMatrix(new Rectangle(Point.Empty, sourceSize));

        /// <summary>
        /// Returns the combined matrix for a given source rectangle.
        /// </summary>
        /// <param name="sourceRectangle">The rectangle in the source image.</param>
        /// <returns>The <see cref="Matrix4x4"/>.</returns>
        public Matrix4x4 BuildMatrix(Rectangle sourceRectangle)
        {
            Guard.MustBeGreaterThan(sourceRectangle.Width, 0, nameof(sourceRectangle));
            Guard.MustBeGreaterThan(sourceRectangle.Height, 0, nameof(sourceRectangle));

            // Translate the origin matrix to cater for source rectangle offsets.
            var matrix = Matrix4x4.CreateTranslation(new Vector3(-sourceRectangle.Location, 0));

            Size size = sourceRectangle.Size;

            foreach (Func<Size, Matrix4x4> factory in this.matrixFactories)
            {
                matrix *= factory(size);
            }

            return matrix;
        }

        private ProjectiveTransformBuilder Prepend(Func<Size, Matrix4x4> factory)
        {
            this.matrixFactories.Insert(0, factory);
            return this;
        }

        private ProjectiveTransformBuilder Append(Func<Size, Matrix4x4> factory)
        {
            this.matrixFactories.Add(factory);
            return this;
        }
    }
}