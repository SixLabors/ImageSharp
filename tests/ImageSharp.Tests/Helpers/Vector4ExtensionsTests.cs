// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class Vector4ExtensionsTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void Premultiply_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v => v.Premultiply()).ToArray();

            Vector4Extensions.Premultiply(source);

            Assert.Equal(expected, source, new ApproximateFloatComparer(1e-6f));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void UnPremultiply_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v => v.UnPremultiply()).ToArray();

            Vector4Extensions.UnPremultiply(source);

            Assert.Equal(expected, source, new ApproximateFloatComparer(1e-6f));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void Expand_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v => v.Expand()).ToArray();

            Vector4Extensions.Expand(source);

            Assert.Equal(expected, source, new ApproximateFloatComparer(1e-6f));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void Compress_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v => v.Compress()).ToArray();

            Vector4Extensions.Compress(source);

            Assert.Equal(expected, source, new ApproximateFloatComparer(1e-6f));
        }
    }
}
