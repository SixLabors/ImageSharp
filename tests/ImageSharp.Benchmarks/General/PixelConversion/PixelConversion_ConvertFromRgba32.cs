// Copyright (c) Six Labors and contributors.
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
            ref uint sBase = ref Unsafe.As<Rgba32, uint>(ref this.PermutedRunnerRgbaToArgb.Source[0]);
            ref uint dBase = ref Unsafe.As<TestArgb, uint>(ref this.PermutedRunnerRgbaToArgb.Dest[0]);

            for (int i = 0; i < this.Count; i++)
            {
                uint s = Unsafe.Add(ref sBase, i);
                Unsafe.Add(ref dBase, i) = PixelConverter.FromRgba32.ToArgb32(s);
            }
        }

        [Benchmark]
        public void PixelConverter_Rgba32_ToArgb32_CopyThenWorkOnSingleBuffer()
        {
            Span<uint> source = MemoryMarshal.Cast<Rgba32, uint>(this.PermutedRunnerRgbaToArgb.Source);
            Span<uint> dest = MemoryMarshal.Cast<TestArgb, uint>(this.PermutedRunnerRgbaToArgb.Dest);
            source.CopyTo(dest);

            ref uint dBase = ref MemoryMarshal.GetReference(dest);

            for (int i = 0; i < this.Count; i++)
            {
                uint s = Unsafe.Add(ref dBase, i);
                Unsafe.Add(ref dBase, i) = PixelConverter.FromRgba32.ToArgb32(s);
            }
        }

        /*
        RESULTS:
                                                            Method | Count |       Mean |      Error |     StdDev | Scaled | ScaledSD |
        ---------------------------------------------------------- |------ |-----------:|-----------:|-----------:|-------:|---------:|
                                                             ByRef |   256 |   328.7 ns |  6.6141 ns |  6.1868 ns |   1.00 |     0.00 |
                                                             ByVal |   256 |   322.0 ns |  4.3541 ns |  4.0728 ns |   0.98 |     0.02 |
                                                         FromBytes |   256 |   321.5 ns |  3.3499 ns |  3.1335 ns |   0.98 |     0.02 |
                                                     InlineShuffle |   256 |   330.7 ns |  4.2525 ns |  3.9778 ns |   1.01 |     0.02 |
                                    PixelConverter_Rgba32_ToArgb32 |   256 |   167.4 ns |  0.6357 ns |  0.5309 ns |   0.51 |     0.01 |
         PixelConverter_Rgba32_ToArgb32_CopyThenWorkOnSingleBuffer |   256 |   196.6 ns |  0.8929 ns |  0.7915 ns |   0.60 |     0.01 |
                                                                   |       |            |            |            |        |          |
                                                             ByRef |  2048 | 2,534.4 ns |  8.2947 ns |  6.9265 ns |   1.00 |     0.00 |
                                                             ByVal |  2048 | 2,638.5 ns | 52.6843 ns | 70.3320 ns |   1.04 |     0.03 |
                                                         FromBytes |  2048 | 2,517.2 ns | 40.8055 ns | 38.1695 ns |   0.99 |     0.01 |
                                                     InlineShuffle |  2048 | 2,546.5 ns | 21.2506 ns | 19.8778 ns |   1.00 |     0.01 |
                                    PixelConverter_Rgba32_ToArgb32 |  2048 | 1,265.7 ns |  5.1397 ns |  4.5562 ns |   0.50 |     0.00 |
         PixelConverter_Rgba32_ToArgb32_CopyThenWorkOnSingleBuffer |  2048 | 1,410.3 ns | 11.1939 ns |  9.9231 ns |   0.56 |     0.00 |
         */
    }
}
