// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace SixLabors.Helpers.Tests
{
    public class GuardTests
    {
        [Fact]
        public void MustBeLessThan_IsLess_ThrowsNoException()
        {
            Guard.MustBeLessThan(0, 1, "myParamName");
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(1, 1)]
        public void MustBeLessThan_IsGreaterOrEqual_ThrowsNoException(int value, int max)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Guard.MustBeLessThan(value, max, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains($"Value must be less than {max}."));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        public void MustBeLessThanOrEqualTo_IsLessOrEqual_ThrowsNoException(int value, int max)
        {
            Guard.MustBeLessThanOrEqualTo(value, max, "myParamName");
        }

        [Fact]
        public void MustBeLessThanOrEqualTo_IsGreater_ThrowsNoException()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Guard.MustBeLessThanOrEqualTo(2, 1, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains($"Value must be less than or equal to 1."));
        }

        [Fact]
        public void MustBeGreaterThan_IsGreater_ThrowsNoException()
        {
            Guard.MustBeGreaterThan(2, 1, "myParamName");
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(1, 1)]
        public void MustBeGreaterThan_IsLessOrEqual_ThrowsNoException(int value, int min)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Guard.MustBeGreaterThan(value, min, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.Contains($"Value must be greater than {min}.", exception.Message);
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(1, 1)]
        public void MustBeGreaterThanOrEqualTo_IsGreaterOrEqual_ThrowsNoException(int value, int min)
        {
            Guard.MustBeGreaterThanOrEqualTo(value, min, "myParamName");
        }

        [Fact]
        public void MustBeGreaterThanOrEqualTo_IsLess_ThrowsNoException()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Guard.MustBeGreaterThanOrEqualTo(1, 2, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains($"Value must be greater than or equal to 2."));
        }

        [Theory]
        [InlineData(1, 1, 3)]
        [InlineData(2, 1, 3)]
        [InlineData(3, 1, 3)]
        public void MustBeBetweenOrEqualTo_IsBetweenOrEqual_ThrowsNoException(int value, int min, int max)
        {
            Guard.MustBeBetweenOrEqualTo(value, min, max, "myParamName");
        }

        [Theory]
        [InlineData(0, 1, 3)]
        [InlineData(4, 1, 3)]
        public void MustBeBetweenOrEqualTo_IsLessOrGreater_ThrowsNoException(int value, int min, int max)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Guard.MustBeBetweenOrEqualTo(value, min, max, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.Contains($"Value must be greater than or equal to {min} and less than or equal to {max}.", exception.Message);
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(2, 2)]
        public void MustBeSizedAtLeast_Array_LengthIsGreaterOrEqual_ThrowsNoException(int valueLength, int minLength)
        {
            Guard.MustBeSizedAtLeast<int>(new int[valueLength], minLength, "myParamName");
        }

        [Fact]
        public void MustBeSizedAtLeast_Array_LengthIsLess_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                Guard.MustBeSizedAtLeast<int>(new int[] { 1, 2 }, 3, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.Contains("The size must be at least 3.", exception.Message);
        }
    }
}
