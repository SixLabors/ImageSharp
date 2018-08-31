// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Tuples;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Common
{
    public class SimdUtilsTests
    {
        private ITestOutputHelper Output { get; }

        public SimdUtilsTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private static int R(float f) => (int)Math.Round(f, MidpointRounding.AwayFromZero);

        private static int Re(float f) => (int)Math.Round(f, MidpointRounding.ToEven);

        // TODO: Move this to a proper test class!
        [Theory]
        [InlineData(0.32, 54.5, -3.5, -4.1)]
        [InlineData(5.3, 536.4, 4.5, 8.1)]
        public void PseudoRound(float x, float y, float z, float w)
        {
            var v = new Vector4(x, y, z, w);

            Vector4 actual = v.PseudoRound();

            Assert.Equal(R(v.X), (int)actual.X);
            Assert.Equal(R(v.Y), (int)actual.Y);
            Assert.Equal(R(v.Z), (int)actual.Z);
            Assert.Equal(R(v.W), (int)actual.W);
        }

        private static Vector<float> CreateExactTestVector1()
        {
            float[] data = new float[Vector<float>.Count];

            data[0] = 0.1f;
            data[1] = 0.4f;
            data[2] = 0.5f;
            data[3] = 0.9f;

            for (int i = 4; i < Vector<float>.Count; i++)
            {
                data[i] = data[i - 4] + 100f;
            }
            return new Vector<float>(data);
        }

        private static Vector<float> CreateRandomTestVector(int seed, float min, float max)
        {
            float[] data = new float[Vector<float>.Count];

            var rnd = new Random();

            for (int i = 0; i < Vector<float>.Count; i++)
            {
                float v = (float)rnd.NextDouble() * (max - min) + min;
                data[i] = v;
            }

            return new Vector<float>(data);
        }

        [Fact]
        public void FastRound()
        {
            Vector<float> v = CreateExactTestVector1();
            Vector<float> r = v.FastRound();

            this.Output.WriteLine(r.ToString());

            AssertEvenRoundIsCorrect(r, v);
        }

        [Theory]
        [InlineData(1, 1f)]
        [InlineData(1, 10f)]
        [InlineData(1, 1000f)]
        [InlineData(42, 1f)]
        [InlineData(42, 10f)]
        [InlineData(42, 1000f)]
        public void FastRound_RandomValues(int seed, float scale)
        {
            Vector<float> v = CreateRandomTestVector(seed, -scale * 0.5f, scale * 0.5f);
            Vector<float> r = v.FastRound();

            this.Output.WriteLine(v.ToString());
            this.Output.WriteLine(r.ToString());

            AssertEvenRoundIsCorrect(r, v);
        }

        private bool SkipOnNonAvx2([CallerMemberName] string testCaseName = null)
        {
            if (!SimdUtils.IsAvx2CompatibleArchitecture)
            {
                this.Output.WriteLine("Skipping AVX2 specific test case: " + testCaseName);
                return true;
            }

            return false;
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, 8)]
        [InlineData(2, 16)]
        [InlineData(3, 128)]
        public void BulkConvertNormalizedFloatToByte_WithRoundedData(int seed, int count)
        {
            if (this.SkipOnNonAvx2())
            {
                return;
            }

            float[] orig = new Random(seed).GenerateRandomRoundedFloatArray(count, 0, 256);
            float[] normalized = orig.Select(f => f / 255f).ToArray();

            byte[] dest = new byte[count];

            SimdUtils.BulkConvertNormalizedFloatToByte(normalized, dest);

            byte[] expected = orig.Select(f => (byte)(f)).ToArray();

            Assert.Equal(expected, dest);
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, 8)]
        [InlineData(2, 16)]
        [InlineData(3, 128)]
        public void BulkConvertNormalizedFloatToByte_WithNonRoundedData(int seed, int count)
        {
            if (this.SkipOnNonAvx2())
            {
                return;
            }

            float[] source = new Random(seed).GenerateRandomFloatArray(count, 0, 1f);

            byte[] dest = new byte[count];

            SimdUtils.BulkConvertNormalizedFloatToByte(source, dest);

            byte[] expected = source.Select(f => (byte)Math.Round(f * 255f)).ToArray();

            Assert.Equal(expected, dest);
        }

        private static float Clamp255(float x) => Math.Min(255f, Math.Max(0f, x));

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, 8)]
        [InlineData(2, 16)]
        [InlineData(3, 128)]
        public void BulkConvertNormalizedFloatToByteClampOverflows(int seed, int count)
        {
            if (this.SkipOnNonAvx2())
            {
                return;
            }

            float[] orig = new Random(seed).GenerateRandomRoundedFloatArray(count, -50, 444);
            float[] normalized = orig.Select(f => f / 255f).ToArray();

            byte[] dest = new byte[count];

            SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(normalized, dest);

            byte[] expected = orig.Select(f => (byte)Clamp255(f)).ToArray();

            Assert.Equal(expected, dest);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(42)]
        [InlineData(255)]
        [InlineData(256)]
        [InlineData(257)]
        private void MagicConvertToByte(float value)
        {
            byte actual = MagicConvert(value / 256f);
            byte expected = (byte)value;

            Assert.Equal(expected, actual);
        }

        [Fact]
        private void BulkConvertNormalizedFloatToByte_Step()
        {
            if (this.SkipOnNonAvx2())
            {
                return;
            }

            float[] source = { 0, 7, 42, 255, 0.5f, 1.1f, 2.6f, 16f };

            var expected = source.Select(f => (byte)Math.Round(f)).ToArray();

            source = source.Select(f => f / 255f).ToArray();

            Span<byte> dest = stackalloc byte[8];

            this.MagicConvert(source, dest);

            Assert.True(dest.SequenceEqual(expected));
        }

        private static byte MagicConvert(float x)
        {
            float f = 32768.0f + x;
            uint i = Unsafe.As<float, uint>(ref f);
            return (byte)i;
        }

        private void MagicConvert(Span<float> source, Span<byte> dest)
        {
            var magick = new Vector<float>(32768.0f);

            Vector<float> scale = new Vector<float>(255f) / new Vector<float>(256f);

            Vector<float> x = MemoryMarshal.Cast<float, Vector<float>>(source)[0];

            x = (x * scale) + magick;

            Tuple8.OfUInt32 ii = default;

            ref Vector<float> iiRef = ref Unsafe.As<Tuple8.OfUInt32, Vector<float>>(ref ii);

            iiRef = x;

            //Tuple8.OfUInt32 ii = Unsafe.As<Vector<float>, Tuple8.OfUInt32>(ref x);

            ref Tuple8.OfByte d = ref MemoryMarshal.Cast<byte, Tuple8.OfByte>(dest)[0];
            d.LoadFrom(ref ii);

            this.Output.WriteLine(ii.ToString());
            this.Output.WriteLine(d.ToString());
        }

        private static void AssertEvenRoundIsCorrect(Vector<float> r, Vector<float> v)
        {
            for (int i = 0; i < Vector<float>.Count; i++)
            {
                int actual = (int)r[i];
                int expected = Re(v[i]);

                Assert.Equal(expected, actual);
            }
        }
    }
}