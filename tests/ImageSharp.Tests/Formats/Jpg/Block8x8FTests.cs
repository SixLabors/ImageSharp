// <copyright file="Block8x8FTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// Uncomment this to turn unit tests into benchmarks:
//#define BENCHMARKING

// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests
{
    using System.Diagnostics;
    using System.Numerics;

    using ImageSharp.Formats;
    using ImageSharp.Formats.Jpg;

    using Xunit;
    using Xunit.Abstractions;

    public class Block8x8FTests : JpegTestBase
    {
#if BENCHMARKING
        public const int Times = 1000000;
#else
        public const int Times = 1;
#endif

        public Block8x8FTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void Indexer()
        {
            float sum = 0;
            this.Measure(
                Times,
                () =>
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
            Assert.Equal(sum, 64f * 63f * 0.5f);
        }

        [Fact]
        public unsafe void Indexer_GetScalarAt_SetScalarAt()
        {
            float sum = 0;
            this.Measure(
                Times,
                () =>
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
            Assert.Equal(sum, 64f * 63f * 0.5f);
        }

        [Fact]
        public void Indexer_ReferenceBenchmarkWithArray()
        {
            float sum = 0;

            this.Measure(
                Times,
                () =>
                    {
                        // Block8x8F block = new Block8x8F();
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
            Assert.Equal(sum, 64f * 63f * 0.5f);
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

            this.Measure(
                Times,
                () =>
                    {
                        Block8x8F b = new Block8x8F();
                        b.LoadFrom(data);
                        b.CopyTo(mirror);
                    });

            Assert.Equal(data, mirror);

            // PrintLinearData((MutableSpan<float>)mirror);
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

            this.Measure(
                Times,
                () =>
                    {
                        Block8x8F b = new Block8x8F();
                        Block8x8F.LoadFrom(&b, data);
                        Block8x8F.CopyTo(&b, mirror);
                    });

            Assert.Equal(data, mirror);

            // PrintLinearData((MutableSpan<float>)mirror);
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

            this.Measure(
                Times,
                () =>
                    {
                        Block8x8F v = new Block8x8F();
                        v.LoadFrom(data);
                        v.CopyTo(mirror);
                    });

            Assert.Equal(data, mirror);

            // PrintLinearData((MutableSpan<int>)mirror);
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

            this.Output.WriteLine($"TranposeInto_PinningImpl_Benchmark X {Times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < Times; i++)
            {
                source.Buffer.TransposeInto(ref dest.Buffer);
            }

            sw.Stop();
            this.Output.WriteLine($"TranposeInto_PinningImpl_Benchmark finished in {sw.ElapsedMilliseconds} ms");
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

            DCT.IDCT8x4_LeftPart(ref source, ref dest);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            this.Print8x8Data(expectedDestArray);
            this.Output.WriteLine("**************");
            this.Print8x8Data(actualDestArray);

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

            DCT.IDCT8x4_RightPart(ref source, ref dest);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            this.Print8x8Data(expectedDestArray);
            this.Output.WriteLine("**************");
            this.Print8x8Data(actualDestArray);

            Assert.Equal(expectedDestArray.Data, actualDestArray);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void TransformIDCT(int seed)
        {
            var sourceArray = Create8x8RandomFloatData(-200, 200, seed);
            float[] expectedDestArray = new float[64];
            float[] tempArray = new float[64];

            ReferenceImplementations.iDCT2D_llm(sourceArray, expectedDestArray, tempArray);

            // ReferenceImplementations.iDCT8x8_llm_sse(sourceArray, expectedDestArray, tempArray);
            Block8x8F source = new Block8x8F();
            source.LoadFrom(sourceArray);

            Block8x8F dest = new Block8x8F();
            Block8x8F tempBuffer = new Block8x8F();

            DCT.TransformIDCT(ref source, ref dest, ref tempBuffer);

            float[] actualDestArray = new float[64];
            dest.CopyTo(actualDestArray);

            this.Print8x8Data(expectedDestArray);
            this.Output.WriteLine("**************");
            this.Print8x8Data(actualDestArray);
            Assert.Equal(expectedDestArray, actualDestArray, new ApproximateFloatComparer(1f));
            Assert.Equal(expectedDestArray, actualDestArray, new ApproximateFloatComparer(1f));
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
            int offset = height * 10 + 20;

            byte[] colorsExpected = new byte[stride * height];
            byte[] colorsActual = new byte[stride * height];

            Block8x8F temp = new Block8x8F();

            ReferenceImplementations.CopyColorsTo(ref block, new MutableSpan<byte>(colorsExpected, offset), stride);

            block.CopyColorsTo(new MutableSpan<byte>(colorsActual, offset), stride, &temp);

            // Output.WriteLine("******* EXPECTED: *********");
            // PrintLinearData(colorsExpected);
            // Output.WriteLine("******** ACTUAL: **********");
            Assert.Equal(colorsExpected, colorsActual);
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
        public void TransformByteConvetibleColorValuesInto()
        {
            Block8x8F block = new Block8x8F();
            var input = Create8x8ColorCropTestData();
            block.LoadFrom(input);
            this.Output.WriteLine("Input:");
            this.PrintLinearData(input);

            Block8x8F dest = new Block8x8F();
            block.TransformByteConvetibleColorValuesInto(ref dest);

            float[] array = new float[64];
            dest.CopyTo(array);
            this.Output.WriteLine("Result:");
            this.PrintLinearData(array);
            foreach (float val in array)
            {
                Assert.InRange(val, 0, 255);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void FDCT8x4_LeftPart(int seed)
        {
            var src = Create8x8RandomFloatData(-200, 200, seed);
            var srcBlock = new Block8x8F();
            srcBlock.LoadFrom(src);

            var destBlock = new Block8x8F();

            var expectedDest = new MutableSpan<float>(64);

            ReferenceImplementations.fDCT2D8x4_32f(src, expectedDest);
            DCT.FDCT8x4_LeftPart(ref srcBlock, ref destBlock);

            var actualDest = new MutableSpan<float>(64);
            destBlock.CopyTo(actualDest);

            Assert.Equal(actualDest.Data, expectedDest.Data, new ApproximateFloatComparer(1f));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void FDCT8x4_RightPart(int seed)
        {
            var src = Create8x8RandomFloatData(-200, 200, seed);
            var srcBlock = new Block8x8F();
            srcBlock.LoadFrom(src);

            var destBlock = new Block8x8F();

            var expectedDest = new MutableSpan<float>(64);

            ReferenceImplementations.fDCT2D8x4_32f(src.Slice(4), expectedDest.Slice(4));
            DCT.FDCT8x4_RightPart(ref srcBlock, ref destBlock);

            var actualDest = new MutableSpan<float>(64);
            destBlock.CopyTo(actualDest);

            Assert.Equal(actualDest.Data, expectedDest.Data, new ApproximateFloatComparer(1f));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TransformFDCT(int seed)
        {
            var src = Create8x8RandomFloatData(-200, 200, seed);
            var srcBlock = new Block8x8F();
            srcBlock.LoadFrom(src);

            var destBlock = new Block8x8F();

            var expectedDest = new MutableSpan<float>(64);
            var temp1 = new MutableSpan<float>(64);
            var temp2 = new Block8x8F();

            ReferenceImplementations.fDCT2D_llm(src, expectedDest, temp1, downscaleBy8: true);
            DCT.TransformFDCT(ref srcBlock, ref destBlock, ref temp2, false);

            var actualDest = new MutableSpan<float>(64);
            destBlock.CopyTo(actualDest);

            Assert.Equal(actualDest.Data, expectedDest.Data, new ApproximateFloatComparer(1f));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public unsafe void UnzigDivRound(int seed)
        {
            Block8x8F block = new Block8x8F();
            block.LoadFrom(Create8x8RandomFloatData(-2000, 2000, seed));

            Block8x8F qt = new Block8x8F();
            qt.LoadFrom(Create8x8RandomFloatData(-2000, 2000, seed));

            UnzigData unzig = UnzigData.Create();

            int* expectedResults = stackalloc int[Block8x8F.ScalarCount];
            ReferenceImplementations.UnZigDivRoundRational(&block, expectedResults, &qt, unzig.Data);

            Block8x8F actualResults = default(Block8x8F);

            Block8x8F.UnzigDivRound(&block, &actualResults, &qt, unzig.Data);

            for (int i = 0; i < Block8x8F.ScalarCount; i++)
            {
                int expected = expectedResults[i];
                int actual = (int)actualResults[i];

                Assert.Equal(expected, actual);
            }
        }
    }
}