// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.



// Uncomment this to turn unit tests into benchmarks:
//#define BENCHMARKING

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System;
    using System.Diagnostics;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

    using Xunit;
    using Xunit.Abstractions;

    public partial class Block8x8FTests : JpegFixture
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
                        var block = new Block8x8F();

                        for (int i = 0; i < Block8x8F.Size; i++)
                        {
                            block[i] = i;
                        }

                        sum = 0;
                        for (int i = 0; i < Block8x8F.Size; i++)
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
                        var block = new Block8x8F();

                        for (int i = 0; i < Block8x8F.Size; i++)
                        {
                            Block8x8F.SetScalarAt(&block, i, i);
                        }

                        sum = 0;
                        for (int i = 0; i < Block8x8F.Size; i++)
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
                        for (int i = 0; i < Block8x8F.Size; i++)
                        {
                            block[i] = i;
                        }

                        sum = 0;
                        for (int i = 0; i < Block8x8F.Size; i++)
                        {
                            sum += block[i];
                        }
                    });
            Assert.Equal(sum, 64f * 63f * 0.5f);
        }

        [Fact]
        public void Load_Store_FloatArray()
        {
            float[] data = new float[Block8x8F.Size];
            float[] mirror = new float[Block8x8F.Size];

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                data[i] = i;
            }

            this.Measure(
                Times,
                () =>
                    {
                        var b = new Block8x8F();
                        b.LoadFrom(data);
                        b.CopyTo(mirror);
                    });

            Assert.Equal(data, mirror);

            // PrintLinearData((Span<float>)mirror);
        }

        [Fact]
        public unsafe void Load_Store_FloatArray_Ptr()
        {
            float[] data = new float[Block8x8F.Size];
            float[] mirror = new float[Block8x8F.Size];

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                data[i] = i;
            }

            this.Measure(
                Times,
                () =>
                    {
                        var b = new Block8x8F();
                        Block8x8F.LoadFrom(&b, data);
                        Block8x8F.CopyTo(&b, mirror);
                    });

            Assert.Equal(data, mirror);

            // PrintLinearData((Span<float>)mirror);
        }

        [Fact]
        public void Load_Store_IntArray()
        {
            int[] data = new int[Block8x8F.Size];
            int[] mirror = new int[Block8x8F.Size];

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                data[i] = i;
            }

            this.Measure(
                Times,
                () =>
                    {
                        var v = new Block8x8F();
                        v.LoadFrom(data);
                        v.CopyTo(mirror);
                    });

            Assert.Equal(data, mirror);

            // PrintLinearData((Span<int>)mirror);
        }

        [Fact]
        public void TransposeInto()
        {
            float[] expected = Create8x8FloatData();
            ReferenceImplementations.Transpose8x8(expected);

            var source = new Block8x8F();
            source.LoadFrom(Create8x8FloatData());

            var dest = new Block8x8F();
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
            var source = new BufferHolder();
            source.Buffer.LoadFrom(Create8x8FloatData());
            var dest = new BufferHolder();

            this.Output.WriteLine($"TranposeInto_PinningImpl_Benchmark X {Times} ...");
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < Times; i++)
            {
                source.Buffer.TransposeInto(ref dest.Buffer);
            }

            sw.Stop();
            this.Output.WriteLine($"TranposeInto_PinningImpl_Benchmark finished in {sw.ElapsedMilliseconds} ms");
        }
        
        [Fact]
        public unsafe void CopyColorsTo()
        {
            float[] data = Create8x8FloatData();
            var block = new Block8x8F();
            block.LoadFrom(data);
            block.MultiplyAllInplace(5);

            int stride = 256;
            int height = 42;
            int offset = height * 10 + 20;

            byte[] colorsExpected = new byte[stride * height];
            byte[] colorsActual = new byte[stride * height];

            var temp = new Block8x8F();

            ReferenceImplementations.CopyColorsTo(ref block, new Span<byte>(colorsExpected, offset), stride);

            block.CopyColorsTo(new Span<byte>(colorsActual, offset), stride, &temp);

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NormalizeColors(bool inplace)
        {
            var block = default(Block8x8F);
            float[] input = Create8x8ColorCropTestData();
            block.LoadFrom(input);
            this.Output.WriteLine("Input:");
            this.PrintLinearData(input);
            
            var dest = default(Block8x8F);

            if (inplace)
            {
                dest = block;
                dest.NormalizeColorsInplace();
            }
            else
            {
                block.NormalizeColorsInto(ref dest);
            }
            

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
        public unsafe void UnzigDivRound(int seed)
        {
            var block = new Block8x8F();
            block.LoadFrom(Create8x8RoundedRandomFloatData(-2000, 2000, seed));

            var qt = new Block8x8F();
            qt.LoadFrom(Create8x8RoundedRandomFloatData(-2000, 2000, seed));

            var unzig = UnzigData.Create();

            int* expectedResults = stackalloc int[Block8x8F.Size];
            ReferenceImplementations.UnZigDivRoundRational(&block, expectedResults, &qt, unzig.Data);

            var actualResults = default(Block8x8F);

            Block8x8F.UnzigDivRound(&block, &actualResults, &qt, unzig.Data);

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                int expected = expectedResults[i];
                int actual = (int)actualResults[i];

                Assert.Equal(expected, actual);
            }
        }
        
        [Fact]
        public void RoundInto()
        {
            float[] data = Create8x8RandomFloatData(-1000, 1000);

            var source = default(Block8x8F);
            source.LoadFrom(data);
            var dest = default(Block8x8);

            source.RoundInto(ref dest);

            for (int i = 0; i < Block8x8.Size; i++)
            {
                float expectedFloat = data[i];
                short expectedShort = (short) Math.Round(expectedFloat);
                short actualShort = dest[i];

                Assert.Equal(expectedShort, actualShort);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void RoundInplace(int seed)
        {
            Block8x8F s = CreateRandomFloatBlock(-500, 500, seed);

            Block8x8F d = s;
            d.RoundInplace();

            this.Output.WriteLine(s.ToString());
            this.Output.WriteLine(d.ToString());

            for (int i = 0; i < 64; i++)
            {
                float expected = MathF.Round(s[i]);
                float actual = d[i];

                Assert.Equal(expected, actual);
            }
        }
    }
}