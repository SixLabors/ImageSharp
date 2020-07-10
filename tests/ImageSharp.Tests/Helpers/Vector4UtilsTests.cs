// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class Vector4UtilsTests
    {
        private readonly ApproximateFloatComparer approximateFloatComparer = new ApproximateFloatComparer(1e-6f);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void Premultiply_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v =>
            {
                Vector4Utilities.Premultiply(ref v);
                return v;
            }).ToArray();

            Vector4Utilities.Premultiply(source);

            Assert.Equal(expected, source, this.approximateFloatComparer);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void UnPremultiply_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v =>
            {
                Vector4Utilities.UnPremultiply(ref v);
                return v;
            }).ToArray();

            Vector4Utilities.UnPremultiply(source);

            Assert.Equal(expected, source, this.approximateFloatComparer);
        }
    }
}
