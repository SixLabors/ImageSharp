// <copyright file="Ellipse.cs" company="Six Labors">
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Collections.Generic;
using System.Numerics;

namespace SixLabors.Primitives
{
    internal struct ApproximateFloatComparer
        : IEqualityComparer<float>,
            IEqualityComparer<Vector4>,
            IEqualityComparer<Vector2>,
            IEqualityComparer<Vector3>,
            IEqualityComparer<Matrix3x2>,
            IEqualityComparer<Matrix4x4>,
            IEqualityComparer<PointF>

    {
        private readonly float tolerance;
        const float defaultTolerance = 1e-5f;

        public ApproximateFloatComparer(float tolerance = defaultTolerance)
        {
            this.tolerance = tolerance;
        }

        public static bool Equal(float x, float y, float tolerance)
        {
            float d = x - y;

            return d > -tolerance && d < tolerance;
        }

        public static bool Equal(float x, float y)
        {
            return Equal(x, y, defaultTolerance);
        }

        public bool Equals(float x, float y)
        {
            return Equal(x, y, this.tolerance);
        }

        public int GetHashCode(float obj)
        {
            var diff = obj % this.tolerance;// how different from tollerance are we?
            return (obj - diff).GetHashCode();
        }

        public bool Equals(Vector4 a, Vector4 b)
        {
            return this.Equals(a.X, b.X) && this.Equals(a.Y, b.Y) && this.Equals(a.Z, b.Z) && this.Equals(a.W, b.W);
        }

        public int GetHashCode(Vector4 obj)
        {
            int hash = GetHashCode(obj.X);
            hash = HashHelpers.Combine(hash, GetHashCode(obj.Y));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.Z));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.W));
            return hash;
        }

        public bool Equals(Vector2 a, Vector2 b)
        {
            return this.Equals(a.X, b.X) && this.Equals(a.Y, b.Y);
        }

        public int GetHashCode(Vector2 obj)
        {
            int hash = GetHashCode(obj.X);
            hash = HashHelpers.Combine(hash, GetHashCode(obj.Y));
            return hash;
        }

        public bool Equals(Vector3 a, Vector3 b)
        {
            return this.Equals(a.X, b.X) && this.Equals(a.Y, b.Y) && this.Equals(a.Z, b.Z);
        }

        public int GetHashCode(Vector3 obj)
        {
            int hash = GetHashCode(obj.X);
            hash = HashHelpers.Combine(hash, GetHashCode(obj.Y));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.Z));
            return hash;
        }

        public static bool Equal(Matrix3x2 a, Matrix3x2 b, float tolerance)
        {
            return Equal(a.M11, b.M11, tolerance) &&
                  Equal(a.M12, b.M12, tolerance) &&
                  Equal(a.M21, b.M21, tolerance) &&
                  Equal(a.M22, b.M22, tolerance) &&
                  Equal(a.M31, b.M31, tolerance) &&
                  Equal(a.M32, b.M32, tolerance);
        }

        public static bool Equal(Matrix3x2 a, Matrix3x2 b)
        {
            return Equal(a, b, defaultTolerance);
        }

        public bool Equals(Matrix3x2 a, Matrix3x2 b)
        {
            return Equal(a, b, this.tolerance);
        }

        public int GetHashCode(Matrix3x2 obj)
        {
            int hash = GetHashCode(obj.M11);
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M11));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M12));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M21));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M22));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M31));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M32));
            return hash;
        }


        public static bool Equal(Matrix4x4 a, Matrix4x4 b, float tolerance)
        {
            return
                  Equal(a.M11, b.M11, tolerance) &&
                  Equal(a.M12, b.M12, tolerance) &&
                  Equal(a.M13, b.M13, tolerance) &&
                  Equal(a.M14, b.M14, tolerance) &&

                  Equal(a.M21, b.M21, tolerance) &&
                  Equal(a.M22, b.M22, tolerance) &&
                  Equal(a.M23, b.M23, tolerance) &&
                  Equal(a.M24, b.M24, tolerance) &&

                  Equal(a.M31, b.M31, tolerance) &&
                  Equal(a.M32, b.M32, tolerance) &&
                  Equal(a.M33, b.M33, tolerance) &&
                  Equal(a.M34, b.M34, tolerance) &&

                  Equal(a.M41, b.M41, tolerance) &&
                  Equal(a.M42, b.M42, tolerance) &&
                  Equal(a.M43, b.M43, tolerance) &&
                  Equal(a.M44, b.M44, tolerance);
        }

        public static bool Equal(Matrix4x4 a, Matrix4x4 b)
        {
            return Equal(a, b, defaultTolerance);
        }

        public bool Equals(Matrix4x4 a, Matrix4x4 b)
        {
            return Equal(a, b, this.tolerance);
        }


        public int GetHashCode(Matrix4x4 obj)
        {
            int hash = GetHashCode(obj.M11);
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M12));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M13));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M14));

            hash = HashHelpers.Combine(hash, GetHashCode(obj.M21));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M22));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M23));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M24));

            hash = HashHelpers.Combine(hash, GetHashCode(obj.M31));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M32));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M33));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M34));

            hash = HashHelpers.Combine(hash, GetHashCode(obj.M41));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M42));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M43));
            hash = HashHelpers.Combine(hash, GetHashCode(obj.M44));
            return hash;
        }


        public static bool Equal(PointF a, PointF b, float tolerance)
        {
            return
                  Equal(a.X, b.X, tolerance) &&
                  Equal(a.Y, b.Y, tolerance);
        }

        public static bool Equal(PointF a, PointF b)
        {
            return Equal(a, b, defaultTolerance);
        }

        public bool Equals(PointF a, PointF b)
        {
            return Equal(a, b, this.tolerance);
        }


        public int GetHashCode(PointF obj)
        {
            int hash = GetHashCode(obj.X);
            hash = HashHelpers.Combine(hash, GetHashCode(obj.Y));
            return hash;
        }
    }
}
