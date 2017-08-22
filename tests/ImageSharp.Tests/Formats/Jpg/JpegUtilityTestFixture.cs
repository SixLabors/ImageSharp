// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Text;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Utils;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    public class JpegUtilityTestFixture : MeasureFixture
    {
        public JpegUtilityTestFixture(ITestOutputHelper output) : base(output)
        {
        }

        // ReSharper disable once InconsistentNaming
        public static float[] Create8x8FloatData()
        {
            float[] result = new float[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[i * 8 + j] = i * 10 + j;
                }
            }
            return result;
        }

        // ReSharper disable once InconsistentNaming
        public static int[] Create8x8IntData()
        {
            int[] result = new int[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[i * 8 + j] = i * 10 + j;
                }
            }
            return result;
        }

        // ReSharper disable once InconsistentNaming
        public static int[] Create8x8RandomIntData(int minValue, int maxValue, int seed = 42)
        {
            Random rnd = new Random(seed);
            int[] result = new int[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[i * 8 + j] = rnd.Next(minValue, maxValue);
                }
            }
            return result;
        }

        internal static float[] Create8x8RandomFloatData(int minValue, int maxValue, int seed = 42)
            => Create8x8RandomIntData(minValue, maxValue, seed).ConvertAllToFloat();

        internal void Print8x8Data<T>(T[] data) => this.Print8x8Data(new Span<T>(data));

        internal void Print8x8Data<T>(Span<T> data)
        {
            StringBuilder bld = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    bld.Append($"{data[i * 8 + j],3} ");
                }
                bld.AppendLine();
            }

            this.Output.WriteLine(bld.ToString());
        }

        internal void PrintLinearData<T>(T[] data) => this.PrintLinearData(new Span<T>(data), data.Length);

        internal void PrintLinearData<T>(Span<T> data, int count = -1)
        {
            if (count < 0) count = data.Length;

            StringBuilder bld = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                bld.Append($"{data[i],3} ");
            }
            this.Output.WriteLine(bld.ToString());
        }

        protected void Print(string msg)
        {
            Debug.WriteLine(msg);
            this.Output.WriteLine(msg);
        }
    }
}