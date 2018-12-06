// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Numerics;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Allows the approximate comparison of single precision floating point values.
    /// </summary>
    internal readonly struct ApproximateFloatComparer :
        IEqualityComparer<float>,
        IEqualityComparer<Vector4>,
        IEqualityComparer<Vector2>
    {
        private readonly float Epsilon;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApproximateFloatComparer"/> class.
        /// </summary>
        /// <param name="epsilon">The comparison error difference epsilon to use.</param>
        public ApproximateFloatComparer(float epsilon = 1f) => this.Epsilon = epsilon;

        /// <inheritdoc/>
        public bool Equals(float x, float y)
        {
            float d = x - y;

            return d >= -this.Epsilon && d <= this.Epsilon;
        }

        /// <inheritdoc/>
        public int GetHashCode(float obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(Vector4 a, Vector4 b) => this.Equals(a.X, b.X) && this.Equals(a.Y, b.Y) && this.Equals(a.Z, b.Z) && this.Equals(a.W, b.W);

        /// <inheritdoc/>
        public int GetHashCode(Vector4 obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(Vector2 a, Vector2 b) => this.Equals(a.X, b.X) && this.Equals(a.Y, b.Y);

        public int GetHashCode(Vector2 obj)
        {
            throw new System.NotImplementedException();
        }
    }
}