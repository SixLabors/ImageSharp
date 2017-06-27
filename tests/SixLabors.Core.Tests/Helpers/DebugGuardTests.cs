// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// tell this file to enable debug conditional method calls, i.e. all the debug guard calls
#define DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Xunit;

namespace SixLabors.Helpers.Tests
{
    public class DebugGuardTests
    {
        [Fact]
        public void AllStaticMethodsOnOnDebugGuardHaveDEBUGConditional()
        {
            var methods = typeof(DebugGuard).GetTypeInfo().GetMethods()
                .Where(x => x.IsStatic);

            foreach (var m in methods)
            {
                var attribs = m.GetCustomAttributes<ConditionalAttribute>();
                Assert.True(attribs.Select(x => x.ConditionString).Contains("DEBUG"), $"Method '{m.Name}' does not have [Conditional(\"DEBUG\")] set.");
            }
        }

        [Fact]
        public void NotNull_TargetNotNull_ThrowsNoException()
        {
            DebugGuard.NotNull("test", "myParamName");
        }

        [Fact]
        public void NotNull_TargetNull_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DebugGuard.NotNull(null, "myParamName");
            });
        }

        [Fact]
        public void MustBeLessThan_IsLess_ThrowsNoException()
        {
            DebugGuard.MustBeLessThan(0, 1, "myParamName");
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(1, 1)]
        public void MustBeLessThan_IsGreaterOrEqual_ThrowsNoException(int value, int max)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                DebugGuard.MustBeLessThan(value, max, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains($"Value must be less than {max}."));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        public void MustBeLessThanOrEqualTo_IsLessOrEqual_ThrowsNoException(int value, int max)
        {
            DebugGuard.MustBeLessThanOrEqualTo(value, max, "myParamName");
        }

        [Fact]
        public void MustBeLessThanOrEqualTo_IsGreater_ThrowsNoException()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                DebugGuard.MustBeLessThanOrEqualTo(2, 1, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains($"Value must be less than or equal to 1."));
        }

        [Fact]
        public void MustBeGreaterThan_IsGreater_ThrowsNoException()
        {
            DebugGuard.MustBeGreaterThan(2, 1, "myParamName");
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(1, 1)]
        public void MustBeGreaterThan_IsLessOrEqual_ThrowsNoException(int value, int min)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                DebugGuard.MustBeGreaterThan(value, min, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains($"Value must be greater than {min}."));
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(1, 1)]
        public void MustBeGreaterThanOrEqualTo_IsGreaterOrEqual_ThrowsNoException(int value, int min)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(value, min, "myParamName");
        }

        [Fact]
        public void MustBeGreaterThanOrEqualTo_IsLess_ThrowsNoException()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                DebugGuard.MustBeGreaterThanOrEqualTo(1, 2, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains($"Value must be greater than or equal to 2."));
        }

        [Fact]
        public void IsTrue_IsTrue_ThrowsNoException()
        {
            DebugGuard.IsTrue(true, "myParamName", "myTestMessage");
        }

        [Fact]
        public void IsTrue_IsFalse_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                DebugGuard.IsTrue(false, "myParamName", "myTestMessage");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains("myTestMessage"));
        }

        [Fact]
        public void IsFalse_IsFalse_ThrowsNoException()
        {
            DebugGuard.IsFalse(false, "myParamName", "myTestMessage");
        }

        [Fact]
        public void IsFalse_IsTrue_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                DebugGuard.IsFalse(true, "myParamName", "myTestMessage");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.True(exception.Message.Contains("myTestMessage"));
        }

        [Theory]
        [InlineData(new int[] { 1, 2 }, 1)]
        [InlineData(new int[] { 1, 2 }, 2)]
        public void MustBeSizedAtLeast_Array_LengthIsGreaterOrEqual_ThrowsNoException(int[] value, int minLength)
        {
            DebugGuard.MustBeSizedAtLeast<int>(value, minLength, "myParamName");
        }

        [Fact]
        public void MustBeSizedAtLeast_Array_LengthIsLess_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                DebugGuard.MustBeSizedAtLeast<int>(new int[] { 1, 2 }, 3, "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.Contains($"The size must be at least 3.", exception.Message);
        }

        [Fact]
        public void MustBeSizedAtLeast_ReadOnlySpan_LengthIsLess_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                DebugGuard.MustBeSizedAtLeast(new ReadOnlySpan<int>(new int[2]), new ReadOnlySpan<int>(new int[3]), "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.Contains($"Span-s must be at least of length 3.", exception.Message);
        }

        [Fact]
        public void MustBeSizedAtLeast_Span_LengthIsLess_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                DebugGuard.MustBeSizedAtLeast(new Span<int>(new int[2]), new Span<int>(new int[3]), "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.Contains($"Span-s must be at least of length 3.", exception.Message);
        }

        [Fact]
        public void MustBeSameSized_ReadOnlySpan_LengthIsLess_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                DebugGuard.MustBeSameSized(new ReadOnlySpan<int>(new int[2]), new ReadOnlySpan<int>(new int[3]), "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.Contains($"Span-s must be the same size.", exception.Message);
        }

        [Fact]
        public void MustBeSameSized_Span_LengthIsLess_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                DebugGuard.MustBeSameSized(new Span<int>(new int[2]), new Span<int>(new int[3]), "myParamName");
            });

            Assert.Equal("myParamName", exception.ParamName);
            Assert.Contains($"Span-s must be the same size.", exception.Message);
        }

        [Theory]
        [InlineData(2, 2)]
        [InlineData(4, 3)]
        public void MustBeSizedAtLeast_ReadOnlySpan_LengthIsEqualOrGreater_DoesNotThowException(int leftSize, int rightSize)
        {
            DebugGuard.MustBeSizedAtLeast(new ReadOnlySpan<int>(new int[leftSize]), new ReadOnlySpan<int>(new int[rightSize]), "myParamName");
        }

        [Theory]
        [InlineData(2, 2)]
        [InlineData(4, 3)]
        public void MustBeSizedAtLeast_Span_LengthIsEqualOrGreater_DoesNotThowException(int leftSize, int rightSize)
        {
            DebugGuard.MustBeSizedAtLeast(new Span<int>(new int[leftSize]), new Span<int>(new int[rightSize]), "myParamName");
        }

        [Fact]
        public void MustBeSameSized_ReadOnlySpan_LengthIsEqual_DoesNotThrowException()
        {
            DebugGuard.MustBeSameSized(new ReadOnlySpan<int>(new int[2]), new ReadOnlySpan<int>(new int[2]), "myParamName");
        }

        [Fact]
        public void MustBeSameSized_Span_LengthIsEqual_DoesNotThrowException()
        {
            DebugGuard.MustBeSameSized(new Span<int>(new int[2]), new Span<int>(new int[2]), "myParamName");
        }
    }
}
