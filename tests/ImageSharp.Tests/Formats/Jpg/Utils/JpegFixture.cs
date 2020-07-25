// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.IO;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    public class JpegFixture : MeasureFixture
    {
        public JpegFixture(ITestOutputHelper output)
            : base(output)
        {
        }

        // ReSharper disable once InconsistentNaming
        public static float[] Create8x8FloatData()
        {
            var result = new float[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[(i * 8) + j] = (i * 10) + j;
                }
            }

            return result;
        }

        // ReSharper disable once InconsistentNaming
        public static int[] Create8x8IntData()
        {
            var result = new int[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[(i * 8) + j] = (i * 10) + j;
                }
            }

            return result;
        }

        // ReSharper disable once InconsistentNaming
        public static short[] Create8x8ShortData()
        {
            var result = new short[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    short val = (short)((i * 10) + j);
                    if ((i + j) % 2 == 0)
                    {
                        val *= -1;
                    }

                    result[(i * 8) + j] = val;
                }
            }

            return result;
        }

        // ReSharper disable once InconsistentNaming
        public static int[] Create8x8RandomIntData(int minValue, int maxValue, int seed = 42)
        {
            var rnd = new Random(seed);
            var result = new int[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[(i * 8) + j] = rnd.Next(minValue, maxValue);
                }
            }

            return result;
        }

        internal static float[] Create8x8RoundedRandomFloatData(int minValue, int maxValue, int seed = 42)
            => Create8x8RandomIntData(minValue, maxValue, seed).ConvertAllToFloat();

        public static float[] Create8x8RandomFloatData(float minValue, float maxValue, int seed = 42)
        {
            var rnd = new Random(seed);
            var result = new float[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    double val = rnd.NextDouble();
                    val *= maxValue - minValue;
                    val += minValue;

                    result[(i * 8) + j] = (float)val;
                }
            }

            return result;
        }

        internal static Block8x8F CreateRandomFloatBlock(float minValue, float maxValue, int seed = 42) =>
            Block8x8F.Load(Create8x8RandomFloatData(minValue, maxValue, seed));

        internal static Block8x8F CreateRoundedRandomFloatBlock(int minValue, int maxValue, int seed = 42) =>
            Block8x8F.Load(Create8x8RoundedRandomFloatData(minValue, maxValue, seed));

        internal void Print8x8Data<T>(T[] data) => this.Print8x8Data(new Span<T>(data));

        internal void Print8x8Data<T>(Span<T> data)
        {
            var bld = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    bld.Append($"{data[(i * 8) + j],3} ");
                }

                bld.AppendLine();
            }

            this.Output.WriteLine(bld.ToString());
        }

        internal void PrintLinearData<T>(T[] data) => this.PrintLinearData(new Span<T>(data), data.Length);

        internal void PrintLinearData<T>(Span<T> data, int count = -1)
        {
            if (count < 0)
            {
                count = data.Length;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                sb.Append($"{data[i],3} ");
            }

            this.Output.WriteLine(sb.ToString());
        }

        protected void Print(string msg)
        {
            Debug.WriteLine(msg);
            this.Output.WriteLine(msg);
        }

        internal void CompareBlocks(Block8x8 a, Block8x8 b, int tolerance) =>
            this.CompareBlocks(a.AsFloatBlock(), b.AsFloatBlock(), tolerance + 1e-5f);

        internal void CompareBlocks(Block8x8F a, Block8x8F b, float tolerance)
            => this.CompareBlocks(a.ToArray(), b.ToArray(), tolerance);

        internal void CompareBlocks(Span<float> a, Span<float> b, float tolerance)
        {
            var comparer = new ApproximateFloatComparer(tolerance);
            double totalDifference = 0.0;

            bool failed = false;

            for (int i = 0; i < 64; i++)
            {
                float expected = a[i];
                float actual = b[i];
                totalDifference += Math.Abs(expected - actual);

                if (!comparer.Equals(expected, actual))
                {
                    failed = true;
                    this.Output.WriteLine($"Difference too large at index {i}");
                }
            }

            this.Output.WriteLine("TOTAL DIFF: " + totalDifference);
            Assert.False(failed);
        }

        internal static JpegDecoderCore ParseJpegStream(string testFileName, bool metaDataOnly = false)
        {
            byte[] bytes = TestFile.Create(testFileName).Bytes;
            using var ms = new MemoryStream(bytes);
            using var bufferedStream = new BufferedReadStream(Configuration.Default, ms);

            var decoder = new JpegDecoderCore(Configuration.Default, new JpegDecoder());
            decoder.ParseStream(bufferedStream, metaDataOnly);

            return decoder;
        }
    }
}
