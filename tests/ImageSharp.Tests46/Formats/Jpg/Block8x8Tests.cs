// Uncomment this to turn unit tests into benchmarks:
#define BENCHMARKING

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests.Formats.Jpg
{
    // ReSharper disable once InconsistentNaming
    public class Block8x8Tests : UtilityTestClassBase
    {
#if BENCHMARKING
        public const int Times = 1000000;
#else
        public const int Times = 1;
#endif

        public Block8x8Tests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Indexer()
        {
            float sum = 0;
            Measure(Times, () =>
            {
                Block8x8 block = new Block8x8();

                for (int i = 0; i < Block8x8.ScalarCount; i++)
                {
                    block[i] = i;
                }
                sum = 0;
                for (int i = 0; i < Block8x8.ScalarCount; i++)
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
                Block8x8 block = new Block8x8();

                for (int i = 0; i < Block8x8.ScalarCount; i++)
                {
                    Block8x8.SetScalarAt(&block, i, i);
                }
                sum = 0;
                for (int i = 0; i < Block8x8.ScalarCount; i++)
                {
                    sum += Block8x8.GetScalarAt(&block, i);
                }
            });
            Assert.Equal(sum, 64f * 63f * 0.5f);
        }

        [Fact]
        public void Indexer_ReferenceBenchmarkWithArray()
        {
            float sum = 0;

            
            Measure(Times, () =>
            {
                //Block8x8 block = new Block8x8();
                float[] block = new float[64];
                for (int i = 0; i < Block8x8.ScalarCount; i++)
                {
                    block[i] = i;
                }
                sum = 0;
                for (int i = 0; i < Block8x8.ScalarCount; i++)
                {
                    sum += block[i];
                }
            });
            Assert.Equal(sum, 64f * 63f * 0.5f);
        }

        [Fact]
        public void Load_Store_FloatArray()
        {
            float[] data = new float[Block8x8.ScalarCount];
            float[] mirror = new float[Block8x8.ScalarCount];

            for (int i = 0; i < Block8x8.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(Times, () =>
            {
                Block8x8 b = new Block8x8();
                b.LoadFrom(data);
                b.CopyTo(mirror);
            });

            Assert.Equal(data, mirror);
            //PrintLinearData((Span<float>)mirror);
        }

        [Fact]
        public unsafe void Load_Store_FloatArray_Ptr()
        {
            float[] data = new float[Block8x8.ScalarCount];
            float[] mirror = new float[Block8x8.ScalarCount];

            for (int i = 0; i < Block8x8.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(Times, () =>
            {
                Block8x8 b = new Block8x8();
                Block8x8.LoadFrom(&b, data);
                Block8x8.CopyTo(&b, mirror);
            });

            Assert.Equal(data, mirror);
            //PrintLinearData((Span<float>)mirror);
        }

        [Fact]
        public void Load_Store_IntArray()
        {
            int[] data = new int[Block8x8.ScalarCount];
            int[] mirror = new int[Block8x8.ScalarCount];

            for (int i = 0; i < Block8x8.ScalarCount; i++)
            {
                data[i] = i;
            }
            Measure(Times, () =>
            {
                Block8x8 v = new Block8x8();
                v.LoadFrom(data);
                v.CopyTo(mirror);
            });

            Assert.Equal(data, mirror);
            //PrintLinearData((Span<int>)mirror);
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

            fixed (Block8x8* sPtr = &source.Buffer)
            {
                fixed (Block8x8* dPtr = &dest.Buffer)
                {
                    Output.WriteLine($"TransposeInto_WithPointers_Benchmark X {Times} ...");
                    Stopwatch sw = Stopwatch.StartNew();

                    for (int i = 0; i < Times; i++)
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
        
    }
}