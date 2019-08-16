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
            var data = new float[Vector<float>.Count];

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
            var data = new float[Vector<float>.Count];

            var rnd = new Random(seed);

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
        public void BasicIntrinsics256_BulkConvertNormalizedFloatToByte_WithRoundedData(int seed, int count)
        {
            if (this.SkipOnNonAvx2())
            {
                return;
            }

            float[] orig = new Random(seed).GenerateRandomRoundedFloatArray(count, 0, 256);
            float[] normalized = orig.Select(f => f / 255f).ToArray();

            var dest = new byte[count];

            SimdUtils.BasicIntrinsics256.BulkConvertNormalizedFloatToByte(normalized, dest);

            byte[] expected = orig.Select(f => (byte)(f)).ToArray();

            Assert.Equal(expected, dest);
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, 8)]
        [InlineData(2, 16)]
        [InlineData(3, 128)]
        public void BasicIntrinsics256_BulkConvertNormalizedFloatToByte_WithNonRoundedData(int seed, int count)
        {
            if (this.SkipOnNonAvx2())
            {
                return;
            }

            float[] source = new Random(seed).GenerateRandomFloatArray(count, 0, 1f);

            var dest = new byte[count];

            SimdUtils.BasicIntrinsics256.BulkConvertNormalizedFloatToByte(source, dest);

            byte[] expected = source.Select(f => (byte)Math.Round(f * 255f)).ToArray();

            Assert.Equal(expected, dest);
        }

        public static readonly TheoryData<int> ArraySizesDivisibleBy8 = new TheoryData<int> { 0, 8, 16, 1024 };
        public static readonly TheoryData<int> ArraySizesDivisibleBy4 = new TheoryData<int> { 0, 4, 8, 28, 1020 };

        public static readonly TheoryData<int> ArraySizesDivisibleBy32 = new TheoryData<int> { 0, 32, 512 };

        public static readonly TheoryData<int> ArbitraryArraySizes =
            new TheoryData<int>
                {
                    0, 1, 2, 3, 4, 7, 8, 9, 15, 16, 17, 63, 64, 255, 511, 512, 513, 514, 515, 516, 517, 518, 519, 520, 520,
                };

        [Theory]
        [MemberData(nameof(ArraySizesDivisibleBy4))]
        public void FallbackIntrinsics128_BulkConvertByteToNormalizedFloat(int count)
        {
            TestImpl_BulkConvertByteToNormalizedFloat(
                count,
                (s, d) => SimdUtils.FallbackIntrinsics128.BulkConvertByteToNormalizedFloat(s.Span, d.Span));
        }

        [Theory]
        [MemberData(nameof(ArraySizesDivisibleBy8))]
        public void BasicIntrinsics256_BulkConvertByteToNormalizedFloat(int count)
        {
            if (this.SkipOnNonAvx2())
            {
                return;
            }

            TestImpl_BulkConvertByteToNormalizedFloat(
                count,
                (s, d) => SimdUtils.BasicIntrinsics256.BulkConvertByteToNormalizedFloat(s.Span, d.Span));
        }

        [Theory]
        [MemberData(nameof(ArraySizesDivisibleBy32))]
        public void ExtendedIntrinsics_BulkConvertByteToNormalizedFloat(int count)
        {
            TestImpl_BulkConvertByteToNormalizedFloat(
                count,
                (s, d) => SimdUtils.ExtendedIntrinsics.BulkConvertByteToNormalizedFloat(s.Span, d.Span));
        }

        [Theory]
        [MemberData(nameof(ArbitraryArraySizes))]
        public void BulkConvertByteToNormalizedFloat(int count)
        {
            TestImpl_BulkConvertByteToNormalizedFloat(
                count,
                (s, d) => SimdUtils.BulkConvertByteToNormalizedFloat(s.Span, d.Span));
        }

        private static void TestImpl_BulkConvertByteToNormalizedFloat(
            int count,
            Action<Memory<byte>, Memory<float>> convert)
        {
            byte[] source = new Random(count).GenerateRandomByteArray(count);
            var result = new float[count];
            float[] expected = source.Select(b => (float)b / 255f).ToArray();

            convert(source, result);

            Assert.Equal(expected, result, new ApproximateFloatComparer(1e-5f));
        }

        [Theory]
        [MemberData(nameof(ArraySizesDivisibleBy4))]
        public void FallbackIntrinsics128_BulkConvertNormalizedFloatToByteClampOverflows(int count)
        {
            TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(count,
                (s, d) => SimdUtils.FallbackIntrinsics128.BulkConvertNormalizedFloatToByteClampOverflows(s.Span, d.Span)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesDivisibleBy8))]
        public void BasicIntrinsics256_BulkConvertNormalizedFloatToByteClampOverflows(int count)
        {
            if (this.SkipOnNonAvx2())
            {
                return;
            }

            TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(count,
                (s, d) => SimdUtils.BasicIntrinsics256.BulkConvertNormalizedFloatToByteClampOverflows(s.Span, d.Span)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesDivisibleBy32))]
        public void ExtendedIntrinsics_BulkConvertNormalizedFloatToByteClampOverflows(int count)
        {
            TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(count,
                (s, d) => SimdUtils.ExtendedIntrinsics.BulkConvertNormalizedFloatToByteClampOverflows(s.Span, d.Span)
            );
        }

        [Theory]
        [InlineData(1234)]
        public void ExtendedIntrinsics_ConvertToSingle(short scale)
        {
            int n = Vector<float>.Count;
            short[] sData = new Random(scale).GenerateRandomInt16Array(2 * n, (short)-scale, scale);
            float[] fData = sData.Select(u => (float)u).ToArray();

            var source = new Vector<short>(sData);

            var expected1 = new Vector<float>(fData, 0);
            var expected2 = new Vector<float>(fData, n);

            // Act:
            SimdUtils.ExtendedIntrinsics.ConvertToSingle(source, out Vector<float> actual1, out Vector<float> actual2);

            // Assert:
            Assert.Equal(expected1, actual1);
            Assert.Equal(expected2, actual2);
        }

        [Theory]
        [MemberData(nameof(ArbitraryArraySizes))]
        public void BulkConvertNormalizedFloatToByteClampOverflows(int count)
        {
            TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(count,
                (s, d) => SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(s.Span, d.Span)
            );

            // for small values, let's stress test the implementation a bit:
            if (count > 0 && count < 10)
            {
                for (int i = 0; i < 20; i++)
                {
                    TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(
                        count,
                        (s, d) => SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(s.Span, d.Span),
                        i + 42);
                }
            }
        }

        private static void TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(
            int count,
            Action<Memory<float>, Memory<byte>> convert, int seed = -1)
        {
            seed = seed > 0 ? seed : count;
            float[] source = new Random(seed).GenerateRandomFloatArray(count, -0.2f, 1.2f);
            byte[] expected = source.Select(NormalizedFloatToByte).ToArray();
            var actual = new byte[count];

            convert(source, actual);

            Assert.Equal(expected, actual);
        }

        private static byte NormalizedFloatToByte(float f) => (byte)Math.Min(255f, Math.Max(0f, f * 255f + 0.5f));

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
            var expected = (byte)value;

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

            byte[] expected = source.Select(f => (byte)Math.Round(f)).ToArray();

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

            var scale = new Vector<float>(255f) / new Vector<float>(256f);

            Vector<float> x = MemoryMarshal.Cast<float, Vector<float>>(source)[0];

            x = (x * scale) + magick;

            Tuple8.OfUInt32 ii = default;

            ref Vector<float> iiRef = ref Unsafe.As<Tuple8.OfUInt32, Vector<float>>(ref ii);

            iiRef = x;

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
