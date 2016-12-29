// <copyright file="UtilityTestClassBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Text;
using ImageSharp.Formats;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    using ImageSharp.Formats.Jpg;

    /// <summary>
    /// Utility class to measure the execution of an operation.
    /// </summary>
    public class MeasureFixture : TestBase
    {
        protected bool EnablePrinting = true;

        protected void Measure(int times, Action action, [CallerMemberName] string operationName = null)
        {
            if (this.EnablePrinting) this.Output?.WriteLine($"{operationName} X {times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                action();
            }

            sw.Stop();
            if (this.EnablePrinting) this.Output?.WriteLine($"{operationName} finished in {sw.ElapsedMilliseconds} ms");
        }

        public MeasureFixture(ITestOutputHelper output)
        {
            this.Output = output;
        }

        protected ITestOutputHelper Output { get; }
    }


    public class UtilityTestClassBase : MeasureFixture
    {
        public UtilityTestClassBase(ITestOutputHelper output) : base(output)
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

        internal static MutableSpan<float> Create8x8RandomFloatData(int minValue, int maxValue, int seed = 42)
            => new MutableSpan<int>(Create8x8RandomIntData(minValue, maxValue, seed)).ConvertToFloat32MutableSpan();

        internal void Print8x8Data<T>(MutableSpan<T> data) => this.Print8x8Data(data.Data);

        internal void Print8x8Data<T>(T[] data)
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

        internal void PrintLinearData<T>(T[] data) => this.PrintLinearData(new MutableSpan<T>(data), data.Length);

        internal void PrintLinearData<T>(MutableSpan<T> data, int count = -1)
        {
            if (count < 0) count = data.TotalCount;

            StringBuilder bld = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                bld.Append($"{data[i],3} ");
            }
            this.Output.WriteLine(bld.ToString());
        }

        internal struct ApproximateFloatComparer : IEqualityComparer<float>
        {
            private readonly float Eps;

            public ApproximateFloatComparer(float eps = 1f)
            {
                this.Eps = eps;
            }

            public bool Equals(float x, float y)
            {
                float d = x - y;

                return d > -this.Eps && d < this.Eps;
            }

            public int GetHashCode(float obj)
            {
                throw new InvalidOperationException();
            }
        }

        protected void Print(string msg)
        {
            Debug.WriteLine(msg);
            this.Output.WriteLine(msg);
        }
    }
}