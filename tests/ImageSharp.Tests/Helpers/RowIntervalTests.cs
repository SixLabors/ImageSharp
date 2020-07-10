// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class RowIntervalTests
    {
        [Fact]
        public void Slice1()
        {
            var rowInterval = new RowInterval(10, 20);
            RowInterval sliced = rowInterval.Slice(5);

            Assert.Equal(15, sliced.Min);
            Assert.Equal(20, sliced.Max);
        }

        [Fact]
        public void Slice2()
        {
            var rowInterval = new RowInterval(10, 20);
            RowInterval sliced = rowInterval.Slice(3, 5);

            Assert.Equal(13, sliced.Min);
            Assert.Equal(18, sliced.Max);
        }

        [Fact]
        public void Equality_WhenTrue()
        {
            var a = new RowInterval(42, 123);
            var b = new RowInterval(42, 123);

            Assert.True(a.Equals(b));
            Assert.True(a.Equals((object)b));
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equality_WhenFalse()
        {
            var a = new RowInterval(42, 123);
            var b = new RowInterval(42, 125);
            var c = new RowInterval(40, 123);

            Assert.False(a.Equals(b));
            Assert.False(c.Equals(a));
            Assert.False(b.Equals(c));

            Assert.False(a.Equals((object)b));
            Assert.False(a.Equals(null));
            Assert.False(a == b);
            Assert.True(a != c);
        }
    }
}
