// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Class to perform simple image comparisons.
    /// </summary>
    public static class VectorAssert
    {
        public static void Equal<TPixel>(TPixel expected, TPixel actual, int precision = int.MaxValue)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Equal(expected.ToVector4(), actual.ToVector4(), precision);
        }

        public static void Equal(Vector4 expected, Vector4 actual, int precision = int.MaxValue)
        {
            Assert.Equal(expected, actual, new PrecisionEqualityComparer(precision));
        }

        public static void Equal(Vector3 expected, Vector3 actual, int precision = int.MaxValue)
        {
            Assert.Equal(expected, actual, new PrecisionEqualityComparer(precision));
        }

        public static void Equal(Vector2 expected, Vector2 actual, int precision = int.MaxValue)
        {
            Assert.Equal(expected, actual, new PrecisionEqualityComparer(precision));
        }

        private struct PrecisionEqualityComparer : IEqualityComparer<float>, IEqualityComparer<Vector4>, IEqualityComparer<Vector3>, IEqualityComparer<Vector2>
        {
            private readonly int precision;

            public PrecisionEqualityComparer(int precision)
            {
                this.precision = precision;
            }

            public bool Equals(Vector2 x, Vector2 y)
            {
                return this.Equals(x.X, y.X) &&
                    this.Equals(x.Y, y.Y);
            }

            public bool Equals(Vector3 x, Vector3 y)
            {
                return this.Equals(x.X, y.X) &&
                    this.Equals(x.Y, y.Y) &&
                    this.Equals(x.Z, y.Z);
            }

            public bool Equals(Vector4 x, Vector4 y)
            {
                return this.Equals(x.W, y.W) &&
                    this.Equals(x.X, y.X) &&
                    this.Equals(x.Y, y.Y) &&
                    this.Equals(x.Z, y.Z);
            }

            public bool Equals(float x, float y)
            {
                return Math.Round(x, this.precision) == Math.Round(y, this.precision);
            }

            public int GetHashCode(Vector4 obj)
            {
                return obj.GetHashCode();
            }

            public int GetHashCode(Vector3 obj)
            {
                return obj.GetHashCode();
            }

            public int GetHashCode(Vector2 obj)
            {
                return obj.GetHashCode();
            }

            public int GetHashCode(float obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
