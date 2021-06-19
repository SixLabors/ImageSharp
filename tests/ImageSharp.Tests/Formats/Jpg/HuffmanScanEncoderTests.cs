// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class HuffmanScanEncoderTests
    {
        private ITestOutputHelper Output { get; }

        public HuffmanScanEncoderTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private static int GetHuffmanEncodingLength_Reference(uint number)
        {
            int bits = 0;
            if (number > 32767)
            {
                number >>= 16;
                bits += 16;
            }

            if (number > 127)
            {
                number >>= 8;
                bits += 8;
            }

            if (number > 7)
            {
                number >>= 4;
                bits += 4;
            }

            if (number > 1)
            {
                number >>= 2;
                bits += 2;
            }

            if (number > 0)
            {
                bits++;
            }

            return bits;
        }

        [Fact]
        public void GetHuffmanEncodingLength_Zero()
        {
            int expected = 0;

            int actual = HuffmanScanEncoder.GetHuffmanEncodingLength(0);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void GetHuffmanEncodingLength_Random(int seed)
        {
            int maxNumber = 1 << 16;

            var rng = new Random(seed);
            for (int i = 0; i < 1000; i++)
            {
                uint number = (uint)rng.Next(0, maxNumber);

                int expected = GetHuffmanEncodingLength_Reference(number);

                int actual = HuffmanScanEncoder.GetHuffmanEncodingLength(number);

                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void GetLastValuableElementIndex_AllZero()
        {
            static void RunTest()
            {
                Block8x8F data = default;

                int expectedLessThan = 1;

                int actual = HuffmanScanEncoder.GetLastValuableElementIndex(ref data);

                Assert.True(actual < expectedLessThan);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
        }

        [Fact]
        public void GetLastValuableElementIndex_AllNonZero()
        {
            static void RunTest()
            {
                Block8x8F data = default;
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    data[i] = 10;
                }

                int expected = Block8x8F.Size - 1;

                int actual = HuffmanScanEncoder.GetLastValuableElementIndex(ref data);

                Assert.Equal(expected, actual);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void GetLastValuableElementIndex_RandomFilledSingle(int seed)
        {
            static void RunTest(string seedSerialized)
            {
                int seed = FeatureTestRunner.Deserialize<int>(seedSerialized);
                var rng = new Random(seed);

                for (int i = 0; i < 1000; i++)
                {
                    Block8x8F data = default;

                    int setIndex = rng.Next(1, Block8x8F.Size);
                    data[setIndex] = rng.Next();

                    int expected = setIndex;

                    int actual = HuffmanScanEncoder.GetLastValuableElementIndex(ref data);

                    Assert.Equal(expected, actual);
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void GetLastValuableElementIndex_RandomFilledPartially(int seed)
        {
            static void RunTest(string seedSerialized)
            {
                int seed = FeatureTestRunner.Deserialize<int>(seedSerialized);
                var rng = new Random(seed);

                for (int i = 0; i < 1000; i++)
                {
                    Block8x8F data = default;

                    int lastIndex = rng.Next(1, Block8x8F.Size);
                    int fillValue = rng.Next();
                    for (int dataIndex = 0; dataIndex <= lastIndex; dataIndex++)
                    {
                        data[dataIndex] = fillValue;
                    }

                    int expected = lastIndex;

                    int actual = HuffmanScanEncoder.GetLastValuableElementIndex(ref data);

                    Assert.Equal(expected, actual);
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void GetLastValuableElementIndex_RandomFilledFragmented(int seed)
        {
            static void RunTest(string seedSerialized)
            {
                int seed = FeatureTestRunner.Deserialize<int>(seedSerialized);
                var rng = new Random(seed);

                for (int i = 0; i < 1000; i++)
                {
                    Block8x8F data = default;

                    int fillValue = rng.Next();

                    // first filled chunk
                    int lastIndex1 = rng.Next(1, Block8x8F.Size / 2);
                    for (int dataIndex = 0; dataIndex <= lastIndex1; dataIndex++)
                    {
                        data[dataIndex] = fillValue;
                    }

                    // second filled chunk, there might be a spot with zero(s) between first and second chunk
                    int lastIndex2 = rng.Next(lastIndex1 + 1, Block8x8F.Size);
                    for (int dataIndex = 0; dataIndex <= lastIndex2; dataIndex++)
                    {
                        data[dataIndex] = fillValue;
                    }

                    int expected = lastIndex2;

                    int actual = HuffmanScanEncoder.GetLastValuableElementIndex(ref data);

                    Assert.Equal(expected, actual);
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
        }
    }
}
