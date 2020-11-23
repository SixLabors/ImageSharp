// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class NumericsTests
    {
        private delegate void SpanAction<T, in TArg, in TArg1>(Span<T> span, TArg arg, TArg1 arg1);

        private readonly ApproximateFloatComparer approximateFloatComparer = new ApproximateFloatComparer(1e-6f);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(100)]
        [InlineData(123)]
        [InlineData(53436353)]
        public void Modulo2(int x)
        {
            int actual = Numerics.Modulo2(x);
            Assert.Equal(x % 2, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(100)]
        [InlineData(123)]
        [InlineData(53436353)]
        public void Modulo4(int x)
        {
            int actual = Numerics.Modulo4(x);
            Assert.Equal(x % 4, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(100)]
        [InlineData(123)]
        [InlineData(53436353)]
        [InlineData(975)]
        public void Modulo8(int x)
        {
            int actual = Numerics.Modulo8(x);
            Assert.Equal(x % 8, actual);
        }

        [Theory]
        [InlineData(0, 2)]
        [InlineData(1, 2)]
        [InlineData(2, 2)]
        [InlineData(0, 4)]
        [InlineData(3, 4)]
        [InlineData(5, 4)]
        [InlineData(5, 8)]
        [InlineData(8, 8)]
        [InlineData(8, 16)]
        [InlineData(15, 16)]
        [InlineData(17, 16)]
        [InlineData(17, 32)]
        [InlineData(31, 32)]
        [InlineData(32, 32)]
        [InlineData(33, 32)]
        public void Modulo2P(int x, int m)
        {
            int actual = Numerics.ModuloP2(x, m);
            Assert.Equal(x % m, actual);
        }

        [Theory]
        [InlineData(-5)]
        [InlineData(-17)]
        [InlineData(-12856)]
        [InlineData(-32)]
        [InlineData(-7425)]
        [InlineData(5)]
        [InlineData(17)]
        [InlineData(12856)]
        [InlineData(32)]
        [InlineData(7425)]
        public void Abs(int x)
        {
            int expected = Math.Abs(x);
            Assert.Equal(expected, Numerics.Abs(x));
        }

        [Theory]
        [InlineData(-5)]
        [InlineData(-17)]
        [InlineData(-12856)]
        [InlineData(-32)]
        [InlineData(-7425)]
        [InlineData(5)]
        [InlineData(17)]
        [InlineData(12856)]
        [InlineData(32)]
        [InlineData(7425)]
        public void Pow2(float x)
        {
            float expected = (float)Math.Pow(x, 2);
            Assert.Equal(expected, Numerics.Pow2(x));
        }

        [Theory]
        [InlineData(-5)]
        [InlineData(-17)]
        [InlineData(-12856)]
        [InlineData(-32)]
        [InlineData(5)]
        [InlineData(17)]
        [InlineData(12856)]
        [InlineData(32)]
        public void Pow3(float x)
        {
            float expected = (float)Math.Pow(x, 3);
            Assert.Equal(expected, Numerics.Pow3(x));
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(1, 42, 1)]
        [InlineData(10, 8, 2)]
        [InlineData(12, 18, 6)]
        [InlineData(4536, 1000, 8)]
        [InlineData(1600, 1024, 64)]
        public void GreatestCommonDivisor(int a, int b, int expected)
        {
            int actual = Numerics.GreatestCommonDivisor(a, b);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(1, 42, 42)]
        [InlineData(3, 4, 12)]
        [InlineData(6, 4, 12)]
        [InlineData(1600, 1024, 25600)]
        [InlineData(3264, 100, 81600)]
        public void LeastCommonMultiple(int a, int b, int expected)
        {
            int actual = Numerics.LeastCommonMultiple(a, b);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(63)]
        public void PremultiplyVectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v =>
            {
                Numerics.Premultiply(ref v);
                return v;
            }).ToArray();

            Numerics.Premultiply(source);

            Assert.Equal(expected, source, this.approximateFloatComparer);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(63)]
        public void UnPremultiplyVectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v =>
            {
                Numerics.UnPremultiply(ref v);
                return v;
            }).ToArray();

            Numerics.UnPremultiply(source);

            Assert.Equal(expected, source, this.approximateFloatComparer);
        }

        [Theory]
        [InlineData(64, 36, 96)]
        [InlineData(128, 16, 196)]
        [InlineData(567, 18, 142)]
        [InlineData(1024, 0, 255)]
        public void ClampByte(int length, byte min, byte max)
        {
            TestClampSpan(
                length,
                min,
                max,
                (s, m1, m2) => Numerics.Clamp(s, m1, m2),
                (v, m1, m2) => Numerics.Clamp(v, m1, m2));
        }

        [Theory]
        [InlineData(64, 36, 96)]
        [InlineData(128, 16, 196)]
        [InlineData(567, 18, 142)]
        [InlineData(1024, 0, 255)]
        public void ClampInt(int length, int min, int max)
        {
            TestClampSpan(
                length,
                min,
                max,
                (s, m1, m2) => Numerics.Clamp(s, m1, m2),
                (v, m1, m2) => Numerics.Clamp(v, m1, m2));
        }

        [Theory]
        [InlineData(64, 36, 96)]
        [InlineData(128, 16, 196)]
        [InlineData(567, 18, 142)]
        [InlineData(1024, 0, 255)]
        public void ClampUInt(int length, uint min, uint max)
        {
            TestClampSpan(
                length,
                min,
                max,
                (s, m1, m2) => Numerics.Clamp(s, m1, m2),
                (v, m1, m2) => Numerics.Clamp(v, m1, m2));
        }

        [Theory]
        [InlineData(64, 36, 96)]
        [InlineData(128, 16, 196)]
        [InlineData(567, 18, 142)]
        [InlineData(1024, 0, 255)]
        public void ClampFloat(int length, float min, float max)
        {
            TestClampSpan(
                length,
                min,
                max,
                (s, m1, m2) => Numerics.Clamp(s, m1, m2),
                (v, m1, m2) => Numerics.Clamp(v, m1, m2));
        }

        [Theory]
        [InlineData(64, 36, 96)]
        [InlineData(128, 16, 196)]
        [InlineData(567, 18, 142)]
        [InlineData(1024, 0, 255)]
        public void ClampDouble(int length, double min, double max)
        {
            TestClampSpan(
                length,
                min,
                max,
                (s, m1, m2) => Numerics.Clamp(s, m1, m2),
                (v, m1, m2) => Numerics.Clamp(v, m1, m2));
        }

        private static void TestClampSpan<T>(
            int length,
            T min,
            T max,
            SpanAction<T, T, T> clampAction,
            Func<T, T, T, T> refClampFunc)
            where T : unmanaged, IComparable<T>
        {
            Span<T> actual = new T[length];

            var r = new Random();
            for (int i = 0; i < length; i++)
            {
                actual[i] = (T)Convert.ChangeType(r.Next(byte.MinValue, byte.MaxValue), typeof(T));
            }

            Span<T> expected = new T[length];
            actual.CopyTo(expected);

            for (int i = 0; i < expected.Length; i++)
            {
                ref T v = ref expected[i];
                v = refClampFunc(v, min, max);
            }

            clampAction(actual, min, max);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
    }
}
