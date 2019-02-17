// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Allows the approximate comparison of single precision floating point values.
    /// </summary>
    internal readonly struct ApproximateFloatComparer :
        IEqualityComparer<float>,
        IEqualityComparer<Vector2>,
        IEqualityComparer<Vector4>,
        IEqualityComparer<ColorMatrix>
    {
        private readonly float Epsilon;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApproximateFloatComparer"/> class.
        /// </summary>
        /// <param name="epsilon">The comparison error difference epsilon to use.</param>
        public ApproximateFloatComparer(float epsilon = 1F) => this.Epsilon = epsilon;

        /// <inheritdoc/>
        public bool Equals(float x, float y)
        {
            float d = x - y;

            return d >= -this.Epsilon && d <= this.Epsilon;
        }

        /// <inheritdoc/>
        public int GetHashCode(float obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(Vector2 x, Vector2 y) => this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y);

        /// <inheritdoc/>
        public int GetHashCode(Vector2 obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(Vector4 x, Vector4 y) => this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y) && this.Equals(x.Z, y.Z) && this.Equals(x.W, y.W);

        /// <inheritdoc/>
        public int GetHashCode(Vector4 obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(ColorMatrix x, ColorMatrix y)
        {
            return
            this.Equals(x.M11, y.M11) && this.Equals(x.M12, y.M12) && this.Equals(x.M13, y.M13) && this.Equals(x.M14, y.M14)
            && this.Equals(x.M21, y.M21) && this.Equals(x.M22, y.M22) && this.Equals(x.M23, y.M23) && this.Equals(x.M24, y.M24)
            && this.Equals(x.M31, y.M31) && this.Equals(x.M32, y.M32) && this.Equals(x.M33, y.M33) && this.Equals(x.M34, y.M34)
            && this.Equals(x.M41, y.M41) && this.Equals(x.M42, y.M42) && this.Equals(x.M43, y.M43) && this.Equals(x.M44, y.M44)
            && this.Equals(x.M51, y.M51) && this.Equals(x.M52, y.M52) && this.Equals(x.M53, y.M53) && this.Equals(x.M54, y.M54);
        }

        /// <inheritdoc/>
        public int GetHashCode(ColorMatrix obj) => obj.GetHashCode();
    }
}