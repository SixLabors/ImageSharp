// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public class PixelConversion_Rgba32_To_Argb32
    {
        private Rgba32[] source;

        private Argb32[] dest;

        [Params(64)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.source = new Rgba32[this.Count];
            this.dest = new Argb32[this.Count];
        }

        [Benchmark(Baseline = true)]
        public void Default()
        {
            ref Rgba32 sBase = ref this.source[0];
            ref Argb32 dBase = ref this.dest[0];

            for (int i = 0; i < this.Count; i++)
            {
                Rgba32 s = Unsafe.Add(ref sBase, i);
                Unsafe.Add(ref dBase, i).FromRgba32(s);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Default_GenericImpl<TPixel>(ReadOnlySpan<Rgba32> source, Span<TPixel> dest)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref Rgba32 sBase = ref MemoryMarshal.GetReference(source);
            ref TPixel dBase = ref MemoryMarshal.GetReference(dest);

            for (int i = 0; i < source.Length; i++)
            {
                Rgba32 s = Unsafe.Add(ref sBase, i);
                Unsafe.Add(ref dBase, i).FromRgba32(s);
            }
        }

        [Benchmark]
        public void Default_Generic()
        {
            Default_GenericImpl(this.source.AsSpan(), this.dest.AsSpan());
        }

        [Benchmark]
        public void Default_Group2()
        {
            ref Rgba32 sBase = ref this.source[0];
            ref Argb32 dBase = ref this.dest[0];

            for (int i = 0; i < this.Count; i += 2)
            {
                ref Rgba32 s0 = ref Unsafe.Add(ref sBase, i);
                Rgba32 s1 = Unsafe.Add(ref s0, 1);

                ref Argb32 d0 = ref Unsafe.Add(ref dBase, i);
                d0.FromRgba32(s0);
                Unsafe.Add(ref d0, 1).FromRgba32(s1);
            }
        }

        [Benchmark]
        public void Default_Group4()
        {
            ref Rgba32 sBase = ref this.source[0];
            ref Argb32 dBase = ref this.dest[0];

            for (int i = 0; i < this.Count; i += 4)
            {
                ref Rgba32 s0 = ref Unsafe.Add(ref sBase, i);
                ref Rgba32 s1 = ref Unsafe.Add(ref s0, 1);
                ref Rgba32 s2 = ref Unsafe.Add(ref s1, 1);
                Rgba32 s3 = Unsafe.Add(ref s2, 1);

                ref Argb32 d0 = ref Unsafe.Add(ref dBase, i);
                ref Argb32 d1 = ref Unsafe.Add(ref d0, 1);
                ref Argb32 d2 = ref Unsafe.Add(ref d1, 1);

                d0.FromRgba32(s0);
                d1.FromRgba32(s1);
                d2.FromRgba32(s2);
                Unsafe.Add(ref d2, 1).FromRgba32(s3);
            }
        }

        [Benchmark]
        public void BitOps()
        {
            ref uint sBase = ref Unsafe.As<Rgba32, uint>(ref this.source[0]);
            ref uint dBase = ref Unsafe.As<Argb32, uint>(ref this.dest[0]);

            for (int i = 0; i < this.Count; i++)
            {
                uint s = Unsafe.Add(ref sBase, i);
                Unsafe.Add(ref dBase, i) = FromRgba32.ToArgb32(s);
            }
        }

        [Benchmark]
        public void BitOps_GroupAsULong()
        {
            ref ulong sBase = ref Unsafe.As<Rgba32, ulong>(ref this.source[0]);
            ref ulong dBase = ref Unsafe.As<Argb32, ulong>(ref this.dest[0]);

            for (int i = 0; i < this.Count / 2; i++)
            {
                ulong s = Unsafe.Add(ref sBase, i);
                uint lo = (uint)s;
                uint hi = (uint)(s >> 32);
                lo = FromRgba32.ToArgb32(lo);
                hi = FromRgba32.ToArgb32(hi);

                s = (ulong)(hi << 32) | lo;

                Unsafe.Add(ref dBase, i) = s;
            }
        }

        public static class FromRgba32
        {
            /// <summary>
            /// Converts a packed <see cref="Rgba32"/> to <see cref="Argb32"/>.
            /// </summary>
            /// <returns>The argb value.</returns>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToArgb32(uint packedRgba)
            {
                // packedRgba          = [aa bb gg rr]
                // ROL(8, packedRgba)  = [bb gg rr aa]
                return (packedRgba << 8) | (packedRgba >> 24);
            }

            /// <summary>
            /// Converts a packed <see cref="Rgba32"/> to <see cref="Bgra32"/>.
            /// </summary>
            /// <returns>The bgra value.</returns>
            [MethodImpl(InliningOptions.ShortMethod)]
            public static uint ToBgra32(uint packedRgba)
            {
                // packedRgba          = [aa bb gg rr]
                // tmp1                = [aa 00 gg 00]
                // tmp2                = [00 bb 00 rr]
                // tmp3=ROL(16, tmp2)  = [00 rr 00 bb]
                // tmp1 + tmp3         = [aa rr gg bb]
                uint tmp1 = packedRgba & 0xFF00FF00;
                uint tmp2 = packedRgba & 0x00FF00FF;
                uint tmp3 = (tmp2 << 16) | (tmp2 >> 16);
                return tmp1 + tmp3;
            }
        }

        // RESULTS:
        //               Method | Count |      Mean |     Error |    StdDev | Scaled |
        // -------------------- |------ |----------:|----------:|----------:|-------:|
        //              Default |    64 | 107.33 ns | 1.0633 ns | 0.9426 ns |   1.00 |
        //      Default_Generic |    64 | 111.15 ns | 0.3789 ns | 0.3544 ns |   1.04 |
        //       Default_Group2 |    64 |  90.36 ns | 0.7779 ns | 0.6896 ns |   0.84 |
        //       Default_Group4 |    64 |  82.39 ns | 0.2726 ns | 0.2550 ns |   0.77 |
        //               BitOps |    64 |  39.25 ns | 0.3266 ns | 0.2895 ns |   0.37 |
        //  BitOps_GroupAsULong |    64 |  41.80 ns | 0.2227 ns | 0.2083 ns |   0.39 |
    }
}
