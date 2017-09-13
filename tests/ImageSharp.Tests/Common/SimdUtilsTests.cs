using System;
using System.Numerics;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Common
{
    using Xunit.Abstractions;

    public class SimdUtilsTests
    {
        private ITestOutputHelper Output { get; }

        public SimdUtilsTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private static int R(float f) => (int)MathF.Round(f, MidpointRounding.AwayFromZero);

        private static int Re(float f) => (int)MathF.Round(f, MidpointRounding.ToEven);

        // TODO: Move this to a proper test class!
        [Theory]
        [InlineData(0.32, 54.5, -3.5, -4.1)]
        [InlineData(5.3, 536.4, 4.5, 8.1)]
        public void PseudoRound(float x, float y, float z, float w)
        {
            var v = new Vector4(x, y, z, w);

            Vector4 actual = v.PseudoRound();

            Assert.Equal(
                R(v.X),
                (int)actual.X
            );
            Assert.Equal(
                R(v.Y),
                (int)actual.Y
            );
            Assert.Equal(
                R(v.Z),
                (int)actual.Z
            );
            Assert.Equal(
                R(v.W),
                (int)actual.W
            );
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

        private static Vector<float> CreateRandomTestVector(int seed, float scale)
        {
            float[] data = new float[Vector<float>.Count];
            Random rnd = new Random();
            for (int i = 0; i < Vector<float>.Count; i++)
            {
                float v = (float)rnd.NextDouble() - 0.5f;
                v *= 2 * scale;
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
            Vector<float> v = CreateRandomTestVector(seed, scale);
            Vector<float> r = v.FastRound();

            this.Output.WriteLine(v.ToString());
            this.Output.WriteLine(r.ToString());

            AssertEvenRoundIsCorrect(r, v);
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