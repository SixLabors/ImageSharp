using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests.Formats.Jpg
{
    public class Buffer64Tests : UtilityTestClassBase
    {
        public Buffer64Tests(ITestOutputHelper output) : base(output)
        {
        }
        
        [Theory]
        [InlineData(1)]
        [InlineData(1000000)]
        public void Load_Store_FloatArray(int times)
        {


            float[] data = new float[Buffer64.ScalarCount];
            float[] mirror = new float[Buffer64.ScalarCount];

            for (int i = 0; i < Buffer64.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(times, () =>
            {
                Buffer64 v = new Buffer64();
                v.LoadFrom(data);
                v.CopyTo(mirror);
            });

            Assert.Equal(data, mirror);
            PrintLinearData((Span<float>)mirror);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1000000)]
        public void Load_Store_IntArray(int times)
        {


            int[] data = new int[Buffer64.ScalarCount];
            int[] mirror = new int[Buffer64.ScalarCount];

            for (int i = 0; i < Buffer64.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(times, () =>
            {
                Buffer64 v = new Buffer64();
                v.LoadFrom(data);
                v.CopyTo(mirror);
            });

            Assert.Equal(data, mirror);
            PrintLinearData((Span<int>)mirror);
        }

        [Fact]
        public void TransposeInplace()
        {
            float[] expected = Create8x8FloatData();
            ReferenceDCT.Transpose8x8(expected);

            Buffer64 buffer = new Buffer64();
            buffer.LoadFrom(Create8x8FloatData());
            
            buffer.TransposeInplace();

            float[] actual = new float[64];
            buffer.CopyTo(actual);

            Assert.Equal(expected, actual);

        }

        [Fact]
        public void TransposeInto()
        {
            float[] expected = Create8x8FloatData();
            ReferenceDCT.Transpose8x8(expected);

            Buffer64 source = new Buffer64();
            source.LoadFrom(Create8x8FloatData());

            Buffer64 dest = new Buffer64();
            source.TranposeInto(ref dest);

            float[] actual = new float[64];
            dest.CopyTo(actual);

            Assert.Equal(expected, actual);
            
        }

        [Fact]
        public void iDCT2D8x4_LeftPart()
        {
            float[] sourceArray = Create8x8FloatData();
            float[] expectedDestArray = new float[64];
            
            ReferenceDCT.iDCT2D8x4_32f(sourceArray, expectedDestArray);
            
            Buffer64 source = new Buffer64();
            source.LoadFrom(sourceArray);

            Buffer64 dest = new Buffer64();

            source.iDCT2D8x4_LeftPart(ref dest);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            Print8x8Data(expectedDestArray);
            Output.WriteLine("**************");
            Print8x8Data(actualDestArray);

            Assert.Equal(expectedDestArray, actualDestArray);
        }

        [Fact]
        public void iDCT2D8x4_RightPart()
        {
            Span<float> sourceArray = Create8x8FloatData();
            Span<float> expectedDestArray = new float[64];

            ReferenceDCT.iDCT2D8x4_32f(sourceArray.Slice(4), expectedDestArray.Slice(4));
            
            Buffer64 source = new Buffer64();
            source.LoadFrom(sourceArray);

            Buffer64 dest = new Buffer64();

            source.iDCT2D8x4_RightPart(ref dest);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            Print8x8Data(expectedDestArray);
            Output.WriteLine("**************");
            Print8x8Data(actualDestArray);

            Assert.Equal(expectedDestArray.Data, actualDestArray);
        }

        [Fact]
        public void IDCT()
        {
            float[] sourceArray = Create8x8FloatData();
            float[] expectedDestArray = new float[64];
            float[] tempArray = new float[64];

            ReferenceDCT.iDCT8x8_llm_sse(sourceArray, expectedDestArray, tempArray);
            
            Buffer64 source = new Buffer64();
            source.LoadFrom(sourceArray);

            Buffer64 dest = new Buffer64();
            Buffer64 tempBuffer = new Buffer64();

            source.TransformIDCTInto(ref dest, ref tempBuffer);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            Print8x8Data(expectedDestArray);
            Output.WriteLine("**************");
            Print8x8Data(actualDestArray);
            Assert.Equal(expectedDestArray, actualDestArray);
        }
    }
}