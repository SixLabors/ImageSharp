// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    public class ClampInt32IntoByte
    {
        [Params(-1, 0, 255, 256)]
        public int Value { get; set; }

        [Benchmark(Baseline = true, Description = "Maths Clamp")]
        public byte ClampMaths()
        {
           int value = this.Value;
           return (byte)Math.Min(Math.Max(byte.MinValue, value), byte.MaxValue);
        }

        [Benchmark(Description = "No Maths Clamp")]
        public byte ClampNoMaths()
        {
           int value = this.Value;
           value = value >= byte.MaxValue ? byte.MaxValue : value;
           return (byte)(value <= byte.MinValue ? byte.MinValue : value);
        }

        [Benchmark(Description = "No Maths No Equals Clamp")]
        public byte ClampNoMathsNoEquals()
        {
           int value = this.Value;
           value = value > byte.MaxValue ? byte.MaxValue : value;
           return (byte)(value < byte.MinValue ? byte.MinValue : value);
        }

        [Benchmark(Description = "No Maths Clamp No Ternary")]
        public byte ClampNoMathsNoTernary()
        {
            int value = this.Value;

            if (value >= byte.MaxValue)
            {
                return byte.MaxValue;
            }

            if (value <= byte.MinValue)
            {
                return byte.MinValue;
            }

            return (byte)value;
        }

        [Benchmark(Description = "No Maths No Equals Clamp No Ternary")]
        public byte ClampNoMathsEqualsNoTernary()
        {
           int value = this.Value;

           if (value > byte.MaxValue)
           {
               return byte.MaxValue;
           }

           if (value < byte.MinValue)
           {
               return byte.MinValue;
           }

           return (byte)value;
        }

        [Benchmark(Description = "Clamp using Bitwise Abs")]
        public byte ClampBitwise()
        {
            int x = this.Value;
            int absMax = byte.MaxValue - x;
            x = (x + byte.MaxValue - AbsBitwiseVer(ref absMax)) >> 1;
            x = (x + byte.MinValue + AbsBitwiseVer(ref x)) >> 1;

            return (byte)x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AbsBitwiseVer(ref int x)
        {
            int y = x >> 31;
            return (x ^ y) - y;
        }
    }
}
