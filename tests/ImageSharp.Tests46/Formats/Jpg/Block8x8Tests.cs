using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests.Formats.Jpg
{
    // ReSharper disable once InconsistentNaming
    public class Block8x8Tests : UtilityTestClassBase
    {
        public Block8x8Tests(ITestOutputHelper output) : base(output)
        {
        }
        
        [Theory]
        [InlineData(1)]
        [InlineData(1000000)]
        public void Load_Store_FloatArray(int times)
        {


            float[] data = new float[Block8x8.ScalarCount];
            float[] mirror = new float[Block8x8.ScalarCount];

            for (int i = 0; i < Block8x8.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(times, () =>
            {
                Block8x8 v = new Block8x8();
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


            int[] data = new int[Block8x8.ScalarCount];
            int[] mirror = new int[Block8x8.ScalarCount];

            for (int i = 0; i < Block8x8.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(times, () =>
            {
                Block8x8 v = new Block8x8();
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

            Block8x8 buffer = new Block8x8();
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
            ReferenceDCT.Transpose8x8(expected);

            Block8x8 source = new Block8x8();
            source.LoadFrom(Create8x8FloatData());

            Block8x8 dest = new Block8x8();
            source.TransposeInto_PinningImpl(ref dest);

            float[] actual = new float[64];
            dest.CopyTo(actual);

            Assert.Equal(expected, actual);    
        }

        [Fact]
        public void TransposeInto()
        {
            float[] expected = Create8x8FloatData();
            ReferenceDCT.Transpose8x8(expected);

            Block8x8 source = new Block8x8();
            source.LoadFrom(Create8x8FloatData());

            Block8x8 dest = new Block8x8();
            source.TransposeInto(ref dest);

            float[] actual = new float[64];
            dest.CopyTo(actual);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Buffer8x8_TransposeInto_GeneratorTest()
        {
            char[] coordz = new[] {'X', 'Y', 'Z', 'W'};
            StringBuilder bld = new StringBuilder();

            for (int i = 0; i < 8; i++)
            {
                char destCoord = coordz[i % 4];
                char destSide = (i / 4) % 2 == 0 ? 'L' : 'R';

                for (int j = 0; j < 8; j++)
                {
                    char srcCoord = coordz[j % 4];
                    char srcSide = (j / 4) % 2 == 0 ? 'L' : 'R';
                    
                    string expression = $"d.V{j}{destSide}.{destCoord} = V{i}{srcSide}.{srcCoord}; ";
                    bld.Append(expression);
                }
                bld.AppendLine();
            }

            Output.WriteLine(bld.ToString());
        }


        [Fact]
        public unsafe void TransposeInto_WithPointers()
        {
            float[] expected = Create8x8FloatData();
            ReferenceDCT.Transpose8x8(expected);

            Block8x8 source = new Block8x8();
            source.LoadFrom(Create8x8FloatData());

            Block8x8 dest = new Block8x8();

            Block8x8* sPtr = &source;
            Block8x8* dPtr = &dest;

            Block8x8.TransposeInto(sPtr, dPtr);

            float[] actual = new float[64];
            dest.CopyTo(actual);

            Assert.Equal(expected, actual);
        }

        private class BufferHolder
        {
            public Block8x8 Buffer;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10000000)]
        public void TranposeInto_Benchmark(int times)
        {
            BufferHolder source = new BufferHolder();
            source.Buffer.LoadFrom(Create8x8FloatData());
            BufferHolder dest = new BufferHolder();

            Output.WriteLine($"TranposeInto_PinningImpl_Benchmark X {times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                source.Buffer.TransposeInto(ref dest.Buffer);
            }

            sw.Stop();
            Output.WriteLine($"TranposeInto_PinningImpl_Benchmark finished in {sw.ElapsedMilliseconds} ms");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10000000)]
        public void TranposeInto_PinningImpl_Benchmark(int times)
        {
            BufferHolder source = new BufferHolder();
            source.Buffer.LoadFrom(Create8x8FloatData());
            BufferHolder dest = new BufferHolder();

            Output.WriteLine($"TranposeInto_PinningImpl_Benchmark X {times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                source.Buffer.TransposeInto_PinningImpl(ref dest.Buffer);
            }

            sw.Stop();
            Output.WriteLine($"TranposeInto_PinningImpl_Benchmark finished in {sw.ElapsedMilliseconds} ms");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10000000)]
        public unsafe void TransposeInto_WithPointers_Benchmark(int times)
        {
            BufferHolder source = new BufferHolder();
            source.Buffer.LoadFrom(Create8x8FloatData());
            BufferHolder dest = new BufferHolder();

            fixed (Block8x8* sPtr = &source.Buffer)
            {
                fixed (Block8x8* dPtr = &dest.Buffer)
                {
                    Output.WriteLine($"TransposeInto_WithPointers_Benchmark X {times} ...");
                    Stopwatch sw = Stopwatch.StartNew();

                    for (int i = 0; i < times; i++)
                    {
                        Block8x8.TransposeInto(sPtr, dPtr);
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
            
            ReferenceDCT.iDCT2D8x4_32f(sourceArray, expectedDestArray);
            
            Block8x8 source = new Block8x8();
            source.LoadFrom(sourceArray);

            Block8x8 dest = new Block8x8();

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
            
            Block8x8 source = new Block8x8();
            source.LoadFrom(sourceArray);

            Block8x8 dest = new Block8x8();

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
            
            Block8x8 source = new Block8x8();
            source.LoadFrom(sourceArray);

            Block8x8 dest = new Block8x8();
            Block8x8 tempBuffer = new Block8x8();

            source.IDCTInto(ref dest, ref tempBuffer);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            Print8x8Data(expectedDestArray);
            Output.WriteLine("**************");
            Print8x8Data(actualDestArray);
            Assert.Equal(expectedDestArray, actualDestArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TransposeMatrix(ref Matrix4x4 s, ref Matrix4x4 d)
        {
            d.M11 = s.M11;
            d.M12 = s.M21;
            d.M13 = s.M31;
            d.M14 = s.M41;
            d.M21 = s.M12;
            d.M22 = s.M22;
            d.M23 = s.M32;
            d.M24 = s.M42;
            d.M31 = s.M13;
            d.M32 = s.M23;
            d.M33 = s.M33;
            d.M34 = s.M43;
            d.M41 = s.M14;
            d.M42 = s.M24;
            d.M43 = s.M34;
            d.M44 = s.M44;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void TransposeMatrixPtr(float* s, float* d)
        {
            for (int i = 0; i < 4; i++)
            {
                int i4 = i*4;
                for (int j = 0; j < 4; j++)
                {
                    d[j*4 + i] = s[i4 + j];
                }
            }
        }




        [Theory]
        [InlineData(50000000)]
        public void TransposeMatrix_Custom(int times)
        {
            Matrix4x4 s = new Matrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
            Matrix4x4 d = new Matrix4x4();

            Output.WriteLine($"TransposeMatrix_System X {times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                TransposeMatrix(ref s, ref d);
            }

            sw.Stop();
            Output.WriteLine($"TransposeMatrix_System finished in {sw.ElapsedMilliseconds} ms");

            Output.WriteLine(d.ToString());
        }



        [Theory]
        [InlineData(50000000)]
        public unsafe void TransposeMatrix_System(int times)
        {
            Matrix4x4 s = new Matrix4x4(1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16);
            Matrix4x4 d = new Matrix4x4();

            Output.WriteLine($"TransposeMatrix_System X {times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                d = Matrix4x4.Transpose(s);
            }

            sw.Stop();
            Output.WriteLine($"TransposeMatrix_System finished in {sw.ElapsedMilliseconds} ms");

            Output.WriteLine(d.ToString());
        }

        [Theory]
        [InlineData(50000000)]
        public unsafe void TransposeMatrix_Ptr(int times)
        {
            Matrix4x4 s = new Matrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
            Matrix4x4 d = new Matrix4x4();

            float* sPtr = (float*) &s;
            float* dPtr = (float*) &d;

            Output.WriteLine($"TransposeMatrix_System X {times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                TransposeMatrixPtr(sPtr,dPtr);
            }

            sw.Stop();
            Output.WriteLine($"TransposeMatrix_System finished in {sw.ElapsedMilliseconds} ms");

            Output.WriteLine(d.ToString());
        }
    }
}