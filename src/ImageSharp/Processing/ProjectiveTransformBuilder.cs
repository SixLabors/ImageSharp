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
        private Rectangle rectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformBuilder"/> class.
        /// </summary>
        /// <param name="sourceSize">The source image size.</param>
        public ProjectiveTransformBuilder(Size sourceSize) => this.Size = sourceSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformBuilder"/> class.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        public ProjectiveTransformBuilder(Rectangle sourceRectangle)
            : this(sourceRectangle.Size)
            => this.rectangle = sourceRectangle;

        /// <summary>
        /// Gets the source image size.
        /// </summary>
        internal Size Size { get; }

        /// <summary>
        /// Prepends a matrix that performs a tapering projective transform.
        /// </summary>
        /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
        /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
        /// <param name="fraction">The amount to taper.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder PrependTaperMatrix(TaperSide side, TaperCorner corner, float fraction)
        {
            this.PrependMatrix(TransformUtils.CreateTaperMatrix(this.Size, side, corner, fraction));
            return this;
        }

        /// <summary>
        /// Appends a matrix that performs a tapering projective transform.
        /// </summary>
        /// <param name="side">An enumeration that indicates the side of the rectangle that tapers.</param>
        /// <param name="corner">An enumeration that indicates on which corners to taper the rectangle.</param>
        /// <param name="fraction">The amount to taper.</param>
        /// <returns>The <see cref="ProjectiveTransformBuilder"/>.</returns>
        public ProjectiveTransformBuilder AppendTaperMatrix(TaperSide side, TaperCorner corner, float fraction)
        {
            this.AppendMatrix(TransformUtils.CreateTaperMatrix(this.Size, side, corner, fraction));
            return this;
        }

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
            if (!this.rectangle.Equals(default))
            {
                matrix *= Matrix4x4.CreateTranslation(new Vector3(-this.rectangle.Location, 0));
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