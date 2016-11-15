// Uncomment this to turn unit tests into benchmarks:
//#define BENCHMARKING

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests.Formats.Jpg
{
    public class Block8x8FTests : UtilityTestClassBase
    {
#if BENCHMARKING
        public const int Times = 1000000;
#else
        public const int Times = 1;
#endif

        public Block8x8FTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Indexer()
        {
            float sum = 0;
            Measure(Times, () =>
            {
                Block8x8F block = new Block8x8F();

                for (int i = 0; i < Block8x8F.ScalarCount; i++)
                {
                    block[i] = i;
                }
                sum = 0;
                for (int i = 0; i < Block8x8F.ScalarCount; i++)
                {
                    sum += block[i];
                }
            });
            Assert.Equal(sum, 64f*63f*0.5f);
        }

        [Fact]
        public unsafe void Indexer_GetScalarAt_SetScalarAt()
        {
            float sum = 0;
            Measure(Times, () =>
            {
                Block8x8F block = new Block8x8F();

                for (int i = 0; i < Block8x8F.ScalarCount; i++)
                {
                    Block8x8F.SetScalarAt(&block, i, i);
                }
                sum = 0;
                for (int i = 0; i < Block8x8F.ScalarCount; i++)
                {
                    sum += Block8x8F.GetScalarAt(&block, i);
                }
            });
            Assert.Equal(sum, 64f*63f*0.5f);
        }

        [Fact]
        public void Indexer_ReferenceBenchmarkWithArray()
        {
            float sum = 0;


            Measure(Times, () =>
            {
                //Block8x8F block = new Block8x8F();
                float[] block = new float[64];
                for (int i = 0; i < Block8x8F.ScalarCount; i++)
                {
                    block[i] = i;
                }
                sum = 0;
                for (int i = 0; i < Block8x8F.ScalarCount; i++)
                {
                    sum += block[i];
                }
            });
            Assert.Equal(sum, 64f*63f*0.5f);
        }

        [Fact]
        public void Load_Store_FloatArray()
        {
            float[] data = new float[Block8x8F.ScalarCount];
            float[] mirror = new float[Block8x8F.ScalarCount];

            for (int i = 0; i < Block8x8F.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(Times, () =>
            {
                Block8x8F b = new Block8x8F();
                b.LoadFrom(data);
                b.CopyTo(mirror);
            });

            Assert.Equal(data, mirror);
            //PrintLinearData((MutableSpan<float>)mirror);
        }

        [Fact]
        public unsafe void Load_Store_FloatArray_Ptr()
        {
            float[] data = new float[Block8x8F.ScalarCount];
            float[] mirror = new float[Block8x8F.ScalarCount];

            for (int i = 0; i < Block8x8F.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(Times, () =>
            {
                Block8x8F b = new Block8x8F();
                Block8x8F.LoadFrom(&b, data);
                Block8x8F.CopyTo(&b, mirror);
            });

            Assert.Equal(data, mirror);
            //PrintLinearData((MutableSpan<float>)mirror);
        }

        [Fact]
        public void Load_Store_IntArray()
        {
            int[] data = new int[Block8x8F.ScalarCount];
            int[] mirror = new int[Block8x8F.ScalarCount];

            for (int i = 0; i < Block8x8F.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(Times, () =>
            {
                Block8x8F v = new Block8x8F();
                v.LoadFrom(data);
                v.CopyTo(mirror);
            });

            Assert.Equal(data, mirror);
            //PrintLinearData((MutableSpan<int>)mirror);
        }

        [Fact]
        public void TransposeInplace()
        {
            float[] expected = Create8x8FloatData();
            ReferenceImplementations.Transpose8x8(expected);

            Block8x8F buffer = new Block8x8F();
            buffer.LoadFrom(Create8x8FloatData());

            buffer.TransposeInplace();

            float[] actual = new float[64];
            buffer.CopyTo(actual);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TranposeInto_PinningImpl()
        {
            float[] expected = Create8x8FloatData();
            ReferenceImplementations.Transpose8x8(expected);

            Block8x8F source = new Block8x8F();
            source.LoadFrom(Create8x8FloatData());

            Block8x8F dest = new Block8x8F();
            source.TransposeInto_PinningImpl(ref dest);

            float[] actual = new float[64];
            dest.CopyTo(actual);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TransposeInto()
        {
            float[] expected = Create8x8FloatData();
            ReferenceImplementations.Transpose8x8(expected);

            Block8x8F source = new Block8x8F();
            source.LoadFrom(Create8x8FloatData());

            Block8x8F dest = new Block8x8F();
            source.TransposeInto(ref dest);

            float[] actual = new float[64];
            dest.CopyTo(actual);

            Assert.Equal(expected, actual);
        }
        

        [Fact]
        public unsafe void TransposeInto_WithPointers()
        {
            float[] expected = Create8x8FloatData();
            ReferenceImplementations.Transpose8x8(expected);

            Block8x8F source = new Block8x8F();
            source.LoadFrom(Create8x8FloatData());

            Block8x8F dest = new Block8x8F();

            Block8x8F* sPtr = &source;
            Block8x8F* dPtr = &dest;

            Block8x8F.TransposeInto(sPtr, dPtr);

            float[] actual = new float[64];
            dest.CopyTo(actual);

            Assert.Equal(expected, actual);
        }

        private class BufferHolder
        {
            public Block8x8F Buffer;
        }

        [Fact]
        public void TranposeInto_Benchmark()
        {
            BufferHolder source = new BufferHolder();
            source.Buffer.LoadFrom(Create8x8FloatData());
            BufferHolder dest = new BufferHolder();

            Output.WriteLine($"TranposeInto_PinningImpl_Benchmark X {Times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < Times; i++)
            {
                source.Buffer.TransposeInto(ref dest.Buffer);
            }

            sw.Stop();
            Output.WriteLine($"TranposeInto_PinningImpl_Benchmark finished in {sw.ElapsedMilliseconds} ms");

        }

        [Fact]
        public void TranposeInto_PinningImpl_Benchmark()
        {
            BufferHolder source = new BufferHolder();
            source.Buffer.LoadFrom(Create8x8FloatData());
            BufferHolder dest = new BufferHolder();

            Output.WriteLine($"TranposeInto_PinningImpl_Benchmark X {Times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < Times; i++)
            {
                source.Buffer.TransposeInto_PinningImpl(ref dest.Buffer);
            }

            sw.Stop();
            Output.WriteLine($"TranposeInto_PinningImpl_Benchmark finished in {sw.ElapsedMilliseconds} ms");
        }

        [Fact]
        public unsafe void TransposeInto_WithPointers_Benchmark()
        {
            BufferHolder source = new BufferHolder();
            source.Buffer.LoadFrom(Create8x8FloatData());
            BufferHolder dest = new BufferHolder();

            fixed (Block8x8F* sPtr = &source.Buffer)
            {
                fixed (Block8x8F* dPtr = &dest.Buffer)
                {
                    Output.WriteLine($"TransposeInto_WithPointers_Benchmark X {Times} ...");
                    Stopwatch sw = Stopwatch.StartNew();

                    for (int i = 0; i < Times; i++)
                    {
                        Block8x8F.TransposeInto(sPtr, dPtr);
                    }

                    sw.Stop();
                    Output.WriteLine($"TransposeInto_WithPointers_Benchmark finished in {sw.ElapsedMilliseconds} ms");
                }
            }

        }


        [Fact]
        public void iDCT2D8x4_LeftPart()
        {
            float[] sourceArray = Create8x8FloatData();
            float[] expectedDestArray = new float[64];

            ReferenceImplementations.iDCT2D8x4_32f(sourceArray, expectedDestArray);

            Block8x8F source = new Block8x8F();
            source.LoadFrom(sourceArray);

            Block8x8F dest = new Block8x8F();

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
            MutableSpan<float> sourceArray = Create8x8FloatData();
            MutableSpan<float> expectedDestArray = new float[64];

            ReferenceImplementations.iDCT2D8x4_32f(sourceArray.Slice(4), expectedDestArray.Slice(4));

            Block8x8F source = new Block8x8F();
            source.LoadFrom(sourceArray);

            Block8x8F dest = new Block8x8F();

            source.iDCT2D8x4_RightPart(ref dest);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            Print8x8Data(expectedDestArray);
            Output.WriteLine("**************");
            Print8x8Data(actualDestArray);

            Assert.Equal(expectedDestArray.Data, actualDestArray);
        }

        private struct ApproximateFloatComparer : IEqualityComparer<float>
        {
            private const float Eps = 0.0001f;

            public bool Equals(float x, float y)
            {
                float d = x - y;

                return d > -Eps && d < Eps;
            }

            public int GetHashCode(float obj)
            {
                throw new InvalidOperationException();
            }
        }

        [Fact]
        public void IDCTInto()
        {
            float[] sourceArray = Create8x8FloatData();
            float[] expectedDestArray = new float[64];
            float[] tempArray = new float[64];

            ReferenceImplementations.iDCT2D_llm(sourceArray, expectedDestArray, tempArray);

            //ReferenceImplementations.iDCT8x8_llm_sse(sourceArray, expectedDestArray, tempArray);

            Block8x8F source = new Block8x8F();
            source.LoadFrom(sourceArray);

            Block8x8F dest = new Block8x8F();
            Block8x8F tempBuffer = new Block8x8F();

            source.IDCTInto(ref dest, ref tempBuffer);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            Print8x8Data(expectedDestArray);
            Output.WriteLine("**************");
            Print8x8Data(actualDestArray);
            Assert.Equal(expectedDestArray, actualDestArray, new ApproximateFloatComparer());
            Assert.Equal(expectedDestArray, actualDestArray, new ApproximateFloatComparer());
        }


        [Fact]
        public unsafe void CopyColorsTo()
        {
            var data = Create8x8FloatData();
            Block8x8F block = new Block8x8F();
            block.LoadFrom(data);
            block.MultiplyAllInplace(new Vector4(5, 5, 5, 5));

            int stride = 256;
            int height = 42;
            int offset = height*10 + 20;

            byte[] colorsExpected = new byte[stride*height];
            byte[] colorsActual = new byte[stride*height];

            Block8x8F temp = new Block8x8F();
            
            ReferenceImplementations.CopyColorsTo(ref block, new MutableSpan<byte>(colorsExpected, offset), stride);

            block.CopyColorsTo(new MutableSpan<byte>(colorsActual, offset), stride, &temp);

            //Output.WriteLine("******* EXPECTED: *********");
            //PrintLinearData(colorsExpected);
            //Output.WriteLine("******** ACTUAL: **********");

            Assert.Equal(colorsExpected, colorsActual);
        }

        [Fact]
        public void CropInto()
        {
            Block8x8F block = new Block8x8F();
            block.LoadFrom(Create8x8FloatData());

            Block8x8F dest = new Block8x8F();
            block.CropInto(10, 20, ref dest);

            float[] array = new float[64];
            dest.CopyTo(array);
            PrintLinearData(array);
            foreach (float val in array)
            {
                Assert.InRange(val, 10, 20);
            }

        }

        private static float[] Create8x8ColorCropTestData()
        {
            float[] result = new float[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[i * 8 + j] = -300 + i * 100 + j * 10;
                }
            }
            return result;
        }

        [Fact]
        public void ColorifyInto()
        {
            Block8x8F block = new Block8x8F();
            var input = Create8x8ColorCropTestData();
            block.LoadFrom(input);
            Output.WriteLine("Input:");
            PrintLinearData(input);
            

            Block8x8F dest = new Block8x8F();
            block.ColorifyInto(ref dest);

            float[] array = new float[64];
            dest.CopyTo(array);
            Output.WriteLine("Result:");
            PrintLinearData(array);
            foreach (float val in array)
            {
                Assert.InRange(val, 0, 255);
            }
        }

    }
}