// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class Vector4UtilsTests
    {
        private readonly ApproximateFloatComparer ApproximateFloatComparer = new ApproximateFloatComparer(1e-6f);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void Premultiply_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v => { Vector4Utils.Premultiply(ref v); return v; }).ToArray();

            Vector4Utils.Premultiply(source);

            Assert.Equal(expected, source, this.ApproximateFloatComparer);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void UnPremultiply_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v => { Vector4Utils.UnPremultiply(ref v); return v; }).ToArray();

            Vector4Utils.UnPremultiply(source);

            Assert.Equal(expected, source, this.ApproximateFloatComparer);
        }
    }
}
