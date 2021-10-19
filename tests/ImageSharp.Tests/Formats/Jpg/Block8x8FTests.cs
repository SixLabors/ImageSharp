// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// Uncomment this to turn unit tests into benchmarks:
// #define BENCHMARKING
using System;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
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

        private bool SkipOnNonAvx2Runner()
        {
            if (!SimdUtils.HasVector8)
            {
                this.Output.WriteLine("AVX2 not supported, skipping!");
                return true;
            }

            return false;
        }

        [Fact]
        public void Indexer()
        {
            float sum = 0;
            this.Measure(
                Times,
                () =>
                {
                    var block = default(Block8x8F);

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
                    var b = default(Block8x8F);
                    b.LoadFrom(data);
                    b.ScaledCopyTo(mirror);
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
                    var b = default(Block8x8F);
                    Block8x8F.LoadFrom(&b, data);
                    Block8x8F.ScaledCopyTo(&b, mirror);
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
                    var v = default(Block8x8F);
                    v.LoadFrom(data);
                    v.ScaledCopyTo(mirror);
                });

            Assert.Equal(data, mirror);

            // PrintLinearData((Span<int>)mirror);
        }

        [Fact]
        public void TransposeInplace()
        {
            static void RunTest()
            {
                float[] expected = Create8x8FloatData();
                ReferenceImplementations.Transpose8x8(expected);

                var block8x8 = default(Block8x8F);
                block8x8.LoadFrom(Create8x8FloatData());

                block8x8.TransposeInplace();

                float[] actual = new float[64];
                block8x8.ScaledCopyTo(actual);

                Assert.Equal(expected, actual);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }

        private static float[] Create8x8ColorCropTestData()
        {
            float[] result = new float[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[(i * 8) + j] = -300 + (i * 100) + (j * 10);
                }
            }

            return result;
        }

        [Fact]
        public void NormalizeColors()
        {
            var block = default(Block8x8F);
            float[] input = Create8x8ColorCropTestData();
            block.LoadFrom(input);
            this.Output.WriteLine("Input:");
            this.PrintLinearData(input);

            Block8x8F dest = block;
            dest.NormalizeColorsInPlace(255);

            float[] array = new float[64];
            dest.ScaledCopyTo(array);
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
        public void NormalizeColorsAndRoundAvx2(int seed)
        {
            if (this.SkipOnNonAvx2Runner())
            {
                return;
            }

            Block8x8F source = CreateRandomFloatBlock(-200, 200, seed);

            Block8x8F expected = source;
            expected.NormalizeColorsInPlace(255);
            expected.RoundInPlace();

            Block8x8F actual = source;
            actual.NormalizeColorsAndRoundInPlaceVector8(255);

            this.Output.WriteLine(expected.ToString());
            this.Output.WriteLine(actual.ToString());
            this.CompareBlocks(expected, actual, 0);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 1)]
        public void Quantize(int srcSeed, int qtSeed)
        {
            static void RunTest(string srcSeedSerialized, string qtSeedSerialized)
            {
                int srcSeed = FeatureTestRunner.Deserialize<int>(srcSeedSerialized);
                int qtSeed = FeatureTestRunner.Deserialize<int>(qtSeedSerialized);

                Block8x8F source = CreateRandomFloatBlock(-2000, 2000, srcSeed);

                // Quantization code is used only in jpeg where it's guaranteed that
                // qunatization valus are greater than 1
                // Quantize method supports negative numbers by very small numbers can cause troubles
                Block8x8F quant = CreateRandomFloatBlock(1, 2000, qtSeed);

                // Reference implementation quantizes given block via division
                Block8x8 expected = default;
                ReferenceImplementations.Quantize(ref source, ref expected, ref quant, ZigZag.ZigZagOrder);

                // Actual current implementation quantizes given block via multiplication
                // With quantization table reciprocal
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    quant[i] = 1f / quant[i];
                }

                Block8x8 actual = default;
                Block8x8F.Quantize(ref source, ref actual, ref quant);

                Assert.True(CompareBlocks(expected, actual, 1, out int diff), $"Blocks are not equal, diff={diff}");
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                srcSeed,
                qtSeed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSE);
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
                short expectedShort = (short)Math.Round(expectedFloat);
                short actualShort = dest[i];

                Assert.Equal(expectedShort, actualShort);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void RoundInPlaceSlow(int seed)
        {
            Block8x8F s = CreateRandomFloatBlock(-500, 500, seed);

            Block8x8F d = s;
            d.RoundInPlace();

            this.Output.WriteLine(s.ToString());
            this.Output.WriteLine(d.ToString());

            for (int i = 0; i < 64; i++)
            {
                float expected = (float)Math.Round(s[i]);
                float actual = d[i];

                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void MultiplyInPlace_ByOtherBlock()
        {
            static void RunTest()
            {
                Block8x8F original = CreateRandomFloatBlock(-500, 500, 42);
                Block8x8F m = CreateRandomFloatBlock(-500, 500, 42);

                Block8x8F actual = original;

                actual.MultiplyInPlace(ref m);

                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    Assert.Equal(original[i] * m[i], actual[i]);
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX);
        }

        [Fact]
        public void AddToAllInPlace()
        {
            static void RunTest()
            {
                Block8x8F original = CreateRandomFloatBlock(-500, 500);

                Block8x8F actual = original;
                actual.AddInPlace(42f);

                for (int i = 0; i < 64; i++)
                {
                    Assert.Equal(original[i] + 42f, actual[i]);
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX);
        }

        [Fact]
        public void MultiplyInPlace_ByScalar()
        {
            static void RunTest()
            {
                Block8x8F original = CreateRandomFloatBlock(-500, 500);

                Block8x8F actual = original;
                actual.MultiplyInPlace(42f);

                for (int i = 0; i < 64; i++)
                {
                    Assert.Equal(original[i] * 42f, actual[i]);
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX);
        }

        [Fact]
        public void LoadFromUInt16Scalar()
        {
            if (this.SkipOnNonAvx2Runner())
            {
                return;
            }

            short[] data = Create8x8ShortData();

            var source = Block8x8.Load(data);

            Block8x8F dest = default;
            dest.LoadFromInt16Scalar(ref source);

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                Assert.Equal(data[i], dest[i]);
            }
        }

        [Fact]
        public void LoadFromUInt16ExtendedAvx2()
        {
            if (this.SkipOnNonAvx2Runner())
            {
                return;
            }

            short[] data = Create8x8ShortData();

            var source = Block8x8.Load(data);

            Block8x8F dest = default;
            dest.LoadFromInt16ExtendedAvx2(ref source);

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                Assert.Equal(data[i], dest[i]);
            }
        }

        [Fact]
        public void EqualsToScalar_AllOne()
        {
            static void RunTest()
            {
                // Fill matrix with valid value
                Block8x8F block = default;
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    block[i] = 1;
                }

                bool isEqual = block.EqualsToScalar(1);
                Assert.True(isEqual);
            }

            // 2 paths:
            // 1. DisableFMA - call avx implementation
            // 3. DisableAvx2 - call fallback code of float implementation
            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
        }

        [Theory]
        [InlineData(10)]
        public void EqualsToScalar_OneOffEachPosition(int equalsTo)
        {
            static void RunTest(string serializedEqualsTo)
            {
                int equalsTo = FeatureTestRunner.Deserialize<int>(serializedEqualsTo);
                int offValue = 0;

                // Fill matrix with valid value
                Block8x8F block = default;
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    block[i] = equalsTo;
                }

                // Assert with invalid values at different positions
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    block[i] = offValue;

                    bool isEqual = block.EqualsToScalar(equalsTo);
                    Assert.False(isEqual, $"False equality:\n{block}");

                    // restore valid value for next iteration assertion
                    block[i] = equalsTo;
                }
            }

            // 2 paths:
            // 1. DisableFMA - call avx implementation
            // 3. DisableAvx2 - call fallback code of float implementation
            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                equalsTo,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
        }

        [Theory]
        [InlineData(39)]
        public void EqualsToScalar_Valid(int equalsTo)
        {
            static void RunTest(string serializedEqualsTo)
            {
                int equalsTo = FeatureTestRunner.Deserialize<int>(serializedEqualsTo);

                // Fill matrix with valid value
                Block8x8F block = default;
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    block[i] = equalsTo;
                }

                // Assert
                bool isEqual = block.EqualsToScalar(equalsTo);
                Assert.True(isEqual);
            }

            // 2 paths:
            // 1. DisableFMA - call avx implementation
            // 3. DisableAvx2 - call fallback code of float implementation
            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                equalsTo,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
        }
    }
}
