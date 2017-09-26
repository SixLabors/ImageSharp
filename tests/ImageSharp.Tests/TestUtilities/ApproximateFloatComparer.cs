// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;

namespace SixLabors.ImageSharp.Tests
{
    internal struct ApproximateFloatComparer : IEqualityComparer<float>, IEqualityComparer<Vector4>
    {
        private readonly float Eps;

        public ApproximateFloatComparer(float eps = 1f)
        {
            this.Eps = eps;
        }

        public bool Equals(float x, float y)
        {
            float d = x - y;

            return d >= -this.Eps && d <= this.Eps;
        }

        public int GetHashCode(float obj)
        {
            throw new InvalidOperationException();
        }

        public bool Equals(Vector4 a, Vector4 b)
        {
            return this.Equals(a.X, b.X) && this.Equals(a.Y, b.Y) && this.Equals(a.Z, b.Z) && this.Equals(a.W, b.W);
        }

        public int GetHashCode(Vector4 obj)
        {
            throw new InvalidOperationException();
        }
    }
}