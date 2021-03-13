// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public abstract class PixelConversion_ConvertFromRgba32
    {
        internal struct ConversionRunner<T>
            where T : struct, ITestPixel<T>
        {
            public readonly T[] Dest;

            public readonly Rgba32[] Source;

            public ConversionRunner(int count)
            {
                this.Dest = new T[count];
                this.Source = new Rgba32[count];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunByRefConversion()
            {
                int count = this.Dest.Length;

                ref T destBaseRef = ref this.Dest[0];
                ref Rgba32 sourceBaseRef = ref this.Source[0];

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref destBaseRef, i).FromRgba32(ref Unsafe.Add(ref sourceBaseRef, i));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunByValConversion()
            {
                int count = this.Dest.Length;

                ref T destBaseRef = ref this.Dest[0];
                ref Rgba32 sourceBaseRef = ref this.Source[0];

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref destBaseRef, i).FromRgba32(Unsafe.Add(ref sourceBaseRef, i));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunFromBytesConversion()
            {
                int count = this.Dest.Length;

                ref T destBaseRef = ref this.Dest[0];
                ref Rgba32 sourceBaseRef = ref this.Source[0];

                for (int i = 0; i < count; i++)
                {
                    ref Rgba32 s = ref Unsafe.Add(ref sourceBaseRef, i);
                    Unsafe.Add(ref destBaseRef, i).FromBytes(s.R, s.G, s.B, s.A);
                }
            }
        }

        internal ConversionRunner<TestRgba> CompatibleMemLayoutRunner;

        internal ConversionRunner<TestArgb> PermutedRunnerRgbaToArgb;

        [Params(256, 2048)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.CompatibleMemLayoutRunner = new ConversionRunner<TestRgba>(this.Count);
            this.PermutedRunnerRgbaToArgb = new ConversionRunner<TestArgb>(this.Count);
        }
    }

    public class PixelConversion_ConvertFromRgba32_Compatible : PixelConversion_ConvertFromRgba32
    {
        [Benchmark(Baseline = true)]
        public void ByRef()
        {
            this.CompatibleMemLayoutRunner.RunByRefConversion();
        }

        [Benchmark]
        public void ByVal()
        {
            this.CompatibleMemLayoutRunner.RunByValConversion();
        }

        [Benchmark]
        public void FromBytes()
        {
            this.CompatibleMemLayoutRunner.RunFromBytesConversion();
        }

        [Benchmark]
        public void Inline()
        {
            ref Rgba32 sBase = ref this.CompatibleMemLayoutRunner.Source[0];
            ref Rgba32 dBase = ref Unsafe.As<TestRgba, Rgba32>(ref this.CompatibleMemLayoutRunner.Dest[0]);

            for (int i = 0; i < this.Count; i++)
            {
                Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, i);
            }
        }

        /*   Method | Count |     Mean |    Error |   StdDev | Scaled | ScaledSD |
         ---------- |------ |---------:|---------:|---------:|-------:|---------:|
              ByRef |   256 | 128.5 ns | 1.217 ns | 1.138 ns |   1.00 |     0.00 |
              ByVal |   256 | 196.7 ns | 2.792 ns | 2.612 ns |   1.53 |     0.02 |
          FromBytes |   256 | 321.7 ns | 2.180 ns | 1.820 ns |   2.50 |     0.03 |
             Inline |   256 | 129.9 ns | 2.759 ns | 2.581 ns |   1.01 |     0.02 | */
    }

    public class PixelConversion_ConvertFromRgba32_Permuted_RgbaToArgb : PixelConversion_ConvertFromRgba32
    {
        [Benchmark(Baseline = true)]
        public void ByRef()
        {
            this.PermutedRunnerRgbaToArgb.RunByRefConversion();
        }

        [Benchmark]
        public void ByVal()
        {
            this.PermutedRunnerRgbaToArgb.RunByValConversion();
        }

        [Benchmark]
        public void FromBytes()
        {
            this.PermutedRunnerRgbaToArgb.RunFromBytesConversion();
        }

        [Benchmark]
        public void InlineShuffle()
        {
            ref Rgba32 sBase = ref this.PermutedRunnerRgbaToArgb.Source[0];
            ref TestArgb dBase = ref this.PermutedRunnerRgbaToArgb.Dest[0];

            for (int i = 0; i < this.Count; i++)
            {
                Rgba32 s = Unsafe.Add(ref sBase, i);
                ref TestArgb d = ref Unsafe.Add(ref dBase, i);

                d.R = s.R;
                d.G = s.G;
                d.B = s.B;
                d.A = s.A;
            }
        }

        [Benchmark]
        public void PixelConverter_Rgba32_ToArgb32()
        {
            Span<byte> source = MemoryMarshal.Cast<Rgba32, byte>(this.PermutedRunnerRgbaToArgb.Source);
            Span<byte> dest = MemoryMarshal.Cast<TestArgb, byte>(this.PermutedRunnerRgbaToArgb.Dest);

            PixelConverter.FromRgba32.ToArgb32(source, dest);
        }

        /*
        RESULTS:
        |                         Method | Count |        Mean |     Error |    StdDev |      Median | Ratio | RatioSD |
        |------------------------------- |------ |------------:|----------:|----------:|------------:|------:|--------:|
        |                          ByRef |   256 |   288.84 ns | 19.601 ns | 52.319 ns |   268.10 ns |  1.00 |    0.00 |
        |                          ByVal |   256 |   267.97 ns |  1.831 ns |  1.713 ns |   267.85 ns |  0.77 |    0.18 |
        |                      FromBytes |   256 |   266.81 ns |  2.427 ns |  2.270 ns |   266.47 ns |  0.76 |    0.18 |
        |                  InlineShuffle |   256 |   291.41 ns |  5.820 ns |  5.444 ns |   290.17 ns |  0.83 |    0.19 |
        | PixelConverter_Rgba32_ToArgb32 |   256 |    38.62 ns |  0.431 ns |  0.403 ns |    38.68 ns |  0.11 |    0.03 |
        |                                |       |             |           |           |             |       |         |
        |                          ByRef |  2048 | 2,197.69 ns | 15.826 ns | 14.804 ns | 2,197.25 ns |  1.00 |    0.00 |
        |                          ByVal |  2048 | 2,226.81 ns | 44.266 ns | 62.054 ns | 2,197.17 ns |  1.03 |    0.04 |
        |                      FromBytes |  2048 | 2,181.35 ns | 18.033 ns | 16.868 ns | 2,185.97 ns |  0.99 |    0.01 |
        |                  InlineShuffle |  2048 | 2,233.10 ns | 27.673 ns | 24.531 ns | 2,229.78 ns |  1.02 |    0.01 |
        | PixelConverter_Rgba32_ToArgb32 |  2048 |   139.90 ns |  2.152 ns |  3.825 ns |   138.70 ns |  0.06 |    0.00 |
        */
    }
}
