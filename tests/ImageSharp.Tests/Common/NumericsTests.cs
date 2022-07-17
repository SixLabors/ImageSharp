// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Common
{
    public class NumericsTests
    {
        private ITestOutputHelper Output { get; }

        public NumericsTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        public static TheoryData<int> IsOutOfRangeTestData = new() { int.MinValue, -1, 0, 1, 6, 7, 8, 91, 92, 93, int.MaxValue };

        private static int Log2_ReferenceImplementation(uint value)
        {
            int n = 0;
            while ((value >>= 1) != 0)
            {
                ++n;
            }

            return n;
        }

        [Fact]
        public void Log2_ZeroConvention()
        {
            uint value = 0;
            int expected = 0;
            int actual = Numerics.Log2(value);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Log2_PowersOfTwo()
        {
            for (int i = 0; i < sizeof(int) * 8; i++)
            {
                // from 2^0 to 2^32
                uint value = (uint)(1 << i);
                int expected = i;
                int actual = Numerics.Log2(value);

                Assert.Equal(expected, actual);
            }
        }

        [Theory]
        [InlineData(1, 100)]
        [InlineData(2, 100)]
        public void Log2_RandomValues(int seed, int count)
        {
            var rng = new Random(seed);
            byte[] bytes = new byte[4];

            for (int i = 0; i < count; i++)
            {
                rng.NextBytes(bytes);
                uint value = BitConverter.ToUInt32(bytes, 0);
                int expected = Log2_ReferenceImplementation(value);
                int actual = Numerics.Log2(value);

                Assert.Equal(expected, actual);
            }
        }

        private static uint DivideCeil_ReferenceImplementation(uint value, uint divisor) => (uint)MathF.Ceiling((float)value / divisor);

        [Fact]
        public void DivideCeil_DivideZero()
        {
            uint expected = 0;
            uint actual = Numerics.DivideCeil(0, 100);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1, 100)]
        public void DivideCeil_RandomValues(int seed, int count)
        {
            var rng = new Random(seed);
            for (int i = 0; i < count; i++)
            {
                uint value = (uint)rng.Next();
                uint divisor = (uint)rng.Next();

                uint expected = DivideCeil_ReferenceImplementation(value, divisor);
                uint actual = Numerics.DivideCeil(value, divisor);

                Assert.True(expected == actual, $"Expected: {expected}\nActual: {actual}\n{value} / {divisor} = {expected}");
            }
        }

        private static bool IsOutOfRange_ReferenceImplementation(int value, int min, int max) => value < min || value > max;

        [Theory]
        [MemberData(nameof(IsOutOfRangeTestData))]
        public void IsOutOfRange(int value)
        {
            const int min = 7;
            const int max = 92;

            bool expected = IsOutOfRange_ReferenceImplementation(value, min, max);
            bool actual = Numerics.IsOutOfRange(value, min, max);

            Assert.True(expected == actual, $"IsOutOfRange({value}, {min}, {max})");
        }
    }
}
