// <copyright file="ArrayCopy.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    /// <summary>
    /// Compare different methods for copying native and/or managed buffers.
    /// Conclusions:
    /// - Span.CopyTo() has terrible performance on classic .NET Framework
    /// - Buffer.MemoryCopy() performance is good enough for all sizes (but needs pinning)
    /// </summary>
    [Config(typeof(Config.ShortClr))]
    public class CopyBuffers
    {
        private byte[] destArray;

        private MemoryHandle destHandle;

        private Memory<byte> destMemory;

        private byte[] sourceArray;

        private MemoryHandle sourceHandle;

        private Memory<byte> sourceMemory;

        [Params(10, 50, 100, 1000, 10000)]
        public int Count { get; set; }
        
        
        [GlobalSetup]
        public void Setup()
        {
            this.sourceArray = new byte[this.Count];
            this.sourceMemory = new Memory<byte>(this.sourceArray);
            this.sourceHandle = this.sourceMemory.Pin();

            this.destArray = new byte[this.Count];
            this.destMemory = new Memory<byte>(this.destArray);
            this.destHandle = this.destMemory.Pin();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.sourceHandle.Dispose();
            this.destHandle.Dispose();
        }

        [Benchmark(Baseline = true, Description = "Array.Copy()")]
        public void ArrayCopy()
        {
            Array.Copy(this.sourceArray, this.destArray, this.Count);
        }

        [Benchmark(Description = "Buffer.BlockCopy()")]
        public void BufferBlockCopy()
        {
            Buffer.BlockCopy(this.sourceArray, 0, this.destArray, 0, this.Count);
        }

        [Benchmark(Description = "Buffer.MemoryCopy()")]
        public unsafe void BufferMemoryCopy()
        {
            void* pinnedDestination = this.destHandle.Pointer;
            void* pinnedSource = this.sourceHandle.Pointer;
            Buffer.MemoryCopy(pinnedSource, pinnedDestination, this.Count, this.Count);
        }


        [Benchmark(Description = "Marshal.Copy()")]
        public unsafe void MarshalCopy()
        {
            void* pinnedDestination = this.destHandle.Pointer;
            Marshal.Copy(this.sourceArray, 0, (IntPtr)pinnedDestination, this.Count);
        }

        [Benchmark(Description = "Span.CopyTo()")]
        public void SpanCopyTo()
        {
            this.sourceMemory.Span.CopyTo(this.destMemory.Span);
        }

        [Benchmark(Description = "Unsafe.CopyBlock()")]
        public unsafe void UnsafeCopyBlock()
        {
            void* pinnedDestination = this.destHandle.Pointer;
            void* pinnedSource = this.sourceHandle.Pointer;
            Unsafe.CopyBlock(pinnedSource, pinnedDestination, (uint)this.Count);
        }
        
        // BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17134.706 (1803/April2018Update/Redstone4)
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742189 Hz, Resolution=364.6722 ns, Timer=TSC
        // .NET Core SDK=2.2.202
        //   [Host] : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //   Clr    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3394.0
        //   Core   : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        // 
        // IterationCount=3  LaunchCount=1  WarmupCount=3  
        // 
        //               Method |  Job | Runtime | Count |       Mean |      Error |    StdDev | Ratio | RatioSD | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
        // -------------------- |----- |-------- |------ |-----------:|-----------:|----------:|------:|--------:|------------:|------------:|------------:|--------------------:|
        //         Array.Copy() |  Clr |     Clr |    10 |  23.579 ns |  1.6836 ns | 0.0923 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() |  Clr |     Clr |    10 |  11.796 ns |  0.5280 ns | 0.0289 ns |  0.50 |    0.00 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() |  Clr |     Clr |    10 |   3.206 ns |  8.1741 ns | 0.4480 ns |  0.14 |    0.02 |           - |           - |           - |                   - |
        //       Marshal.Copy() |  Clr |     Clr |    10 |  15.577 ns |  2.0937 ns | 0.1148 ns |  0.66 |    0.00 |           - |           - |           - |                   - |
        //        Span.CopyTo() |  Clr |     Clr |    10 |  32.287 ns |  2.4107 ns | 0.1321 ns |  1.37 |    0.01 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() |  Clr |     Clr |    10 |   3.266 ns |  0.3848 ns | 0.0211 ns |  0.14 |    0.00 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() | Core |    Core |    10 |  19.713 ns |  7.3026 ns | 0.4003 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() | Core |    Core |    10 |   7.332 ns |  0.5465 ns | 0.0300 ns |  0.37 |    0.01 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() | Core |    Core |    10 |   2.476 ns |  0.3476 ns | 0.0191 ns |  0.13 |    0.00 |           - |           - |           - |                   - |
        //       Marshal.Copy() | Core |    Core |    10 |  15.575 ns |  0.1335 ns | 0.0073 ns |  0.79 |    0.02 |           - |           - |           - |                   - |
        //        Span.CopyTo() | Core |    Core |    10 |  25.321 ns |  2.3556 ns | 0.1291 ns |  1.28 |    0.02 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() | Core |    Core |    10 |   2.204 ns |  0.1836 ns | 0.0101 ns |  0.11 |    0.00 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() |  Clr |     Clr |    50 |  35.217 ns |  2.7642 ns | 0.1515 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() |  Clr |     Clr |    50 |  24.224 ns |  0.8737 ns | 0.0479 ns |  0.69 |    0.00 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() |  Clr |     Clr |    50 |   3.827 ns |  4.8733 ns | 0.2671 ns |  0.11 |    0.01 |           - |           - |           - |                   - |
        //       Marshal.Copy() |  Clr |     Clr |    50 |  28.103 ns |  1.3570 ns | 0.0744 ns |  0.80 |    0.00 |           - |           - |           - |                   - |
        //        Span.CopyTo() |  Clr |     Clr |    50 |  34.137 ns |  2.9274 ns | 0.1605 ns |  0.97 |    0.01 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() |  Clr |     Clr |    50 |   4.999 ns |  0.1778 ns | 0.0097 ns |  0.14 |    0.00 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() | Core |    Core |    50 |  20.925 ns |  1.0219 ns | 0.0560 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() | Core |    Core |    50 |   8.083 ns |  0.2158 ns | 0.0118 ns |  0.39 |    0.00 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() | Core |    Core |    50 |   2.919 ns |  0.2878 ns | 0.0158 ns |  0.14 |    0.00 |           - |           - |           - |                   - |
        //       Marshal.Copy() | Core |    Core |    50 |  16.663 ns |  0.2505 ns | 0.0137 ns |  0.80 |    0.00 |           - |           - |           - |                   - |
        //        Span.CopyTo() | Core |    Core |    50 |  26.940 ns | 11.5855 ns | 0.6350 ns |  1.29 |    0.03 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() | Core |    Core |    50 |   1.940 ns |  0.6327 ns | 0.0347 ns |  0.09 |    0.00 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() |  Clr |     Clr |   100 |  39.284 ns |  0.5647 ns | 0.0310 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() |  Clr |     Clr |   100 |  28.930 ns |  0.6774 ns | 0.0371 ns |  0.74 |    0.00 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() |  Clr |     Clr |   100 |   5.859 ns |  2.7931 ns | 0.1531 ns |  0.15 |    0.00 |           - |           - |           - |                   - |
        //       Marshal.Copy() |  Clr |     Clr |   100 |  36.529 ns |  0.9886 ns | 0.0542 ns |  0.93 |    0.00 |           - |           - |           - |                   - |
        //        Span.CopyTo() |  Clr |     Clr |   100 |  36.152 ns |  1.5109 ns | 0.0828 ns |  0.92 |    0.00 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() |  Clr |     Clr |   100 |   9.317 ns |  0.4342 ns | 0.0238 ns |  0.24 |    0.00 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() | Core |    Core |   100 |  22.899 ns |  8.4066 ns | 0.4608 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() | Core |    Core |   100 |  10.696 ns |  0.8106 ns | 0.0444 ns |  0.47 |    0.01 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() | Core |    Core |   100 |   4.102 ns |  0.9040 ns | 0.0496 ns |  0.18 |    0.01 |           - |           - |           - |                   - |
        //       Marshal.Copy() | Core |    Core |   100 |  17.917 ns |  2.6490 ns | 0.1452 ns |  0.78 |    0.01 |           - |           - |           - |                   - |
        //        Span.CopyTo() | Core |    Core |   100 |  28.247 ns |  0.6375 ns | 0.0349 ns |  1.23 |    0.03 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() | Core |    Core |   100 |   3.611 ns |  0.4792 ns | 0.0263 ns |  0.16 |    0.00 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() |  Clr |     Clr |  1000 |  48.907 ns |  4.4228 ns | 0.2424 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() |  Clr |     Clr |  1000 |  40.653 ns |  1.4055 ns | 0.0770 ns |  0.83 |    0.01 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() |  Clr |     Clr |  1000 |  24.720 ns |  1.2651 ns | 0.0693 ns |  0.51 |    0.00 |           - |           - |           - |                   - |
        //       Marshal.Copy() |  Clr |     Clr |  1000 |  42.336 ns |  2.2466 ns | 0.1231 ns |  0.87 |    0.00 |           - |           - |           - |                   - |
        //        Span.CopyTo() |  Clr |     Clr |  1000 |  70.735 ns |  2.6215 ns | 0.1437 ns |  1.45 |    0.01 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() |  Clr |     Clr |  1000 |  44.520 ns |  0.9641 ns | 0.0528 ns |  0.91 |    0.00 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() | Core |    Core |  1000 |  46.286 ns | 11.6373 ns | 0.6379 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() | Core |    Core |  1000 |  34.243 ns |  7.2264 ns | 0.3961 ns |  0.74 |    0.01 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() | Core |    Core |  1000 |  23.135 ns |  0.3153 ns | 0.0173 ns |  0.50 |    0.01 |           - |           - |           - |                   - |
        //       Marshal.Copy() | Core |    Core |  1000 |  46.219 ns |  1.2869 ns | 0.0705 ns |  1.00 |    0.01 |           - |           - |           - |                   - |
        //        Span.CopyTo() | Core |    Core |  1000 |  45.371 ns |  3.3581 ns | 0.1841 ns |  0.98 |    0.02 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() | Core |    Core |  1000 |  29.347 ns |  1.1349 ns | 0.0622 ns |  0.63 |    0.01 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() |  Clr |     Clr | 10000 | 218.445 ns |  9.2567 ns | 0.5074 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() |  Clr |     Clr | 10000 | 209.610 ns |  6.7447 ns | 0.3697 ns |  0.96 |    0.00 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() |  Clr |     Clr | 10000 | 213.061 ns | 66.6490 ns | 3.6533 ns |  0.98 |    0.02 |           - |           - |           - |                   - |
        //       Marshal.Copy() |  Clr |     Clr | 10000 | 214.426 ns | 27.7722 ns | 1.5223 ns |  0.98 |    0.00 |           - |           - |           - |                   - |
        //        Span.CopyTo() |  Clr |     Clr | 10000 | 486.728 ns | 12.1537 ns | 0.6662 ns |  2.23 |    0.00 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() |  Clr |     Clr | 10000 | 452.973 ns | 25.1490 ns | 1.3785 ns |  2.07 |    0.00 |           - |           - |           - |                   - |
        //                      |      |         |       |            |            |           |       |         |             |             |             |                     |
        //         Array.Copy() | Core |    Core | 10000 | 203.365 ns |  3.2200 ns | 0.1765 ns |  1.00 |    0.00 |           - |           - |           - |                   - |
        //   Buffer.BlockCopy() | Core |    Core | 10000 | 193.319 ns |  8.3370 ns | 0.4570 ns |  0.95 |    0.00 |           - |           - |           - |                   - |
        //  Buffer.MemoryCopy() | Core |    Core | 10000 | 196.541 ns | 37.8056 ns | 2.0723 ns |  0.97 |    0.01 |           - |           - |           - |                   - |
        //       Marshal.Copy() | Core |    Core | 10000 | 206.454 ns |  3.7652 ns | 0.2064 ns |  1.02 |    0.00 |           - |           - |           - |                   - |
        //        Span.CopyTo() | Core |    Core | 10000 | 214.799 ns |  3.0667 ns | 0.1681 ns |  1.06 |    0.00 |           - |           - |           - |                   - |
        //   Unsafe.CopyBlock() | Core |    Core | 10000 | 134.428 ns |  2.6024 ns | 0.1426 ns |  0.66 |    0.00 |           - |           - |           - |                   - |

    }
}