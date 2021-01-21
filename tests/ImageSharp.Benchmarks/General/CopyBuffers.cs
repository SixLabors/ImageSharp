// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
    [Config(typeof(Config.ShortMultiFramework))]
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

        [Benchmark(Description = "Unsafe.CopyBlock(ref)")]
        public void UnsafeCopyBlockReferences()
        {
            Unsafe.CopyBlock(ref this.destArray[0], ref this.sourceArray[0], (uint)this.Count);
        }

        [Benchmark(Description = "Unsafe.CopyBlock(ptr)")]
        public unsafe void UnsafeCopyBlockPointers()
        {
            void* pinnedDestination = this.destHandle.Pointer;
            void* pinnedSource = this.sourceHandle.Pointer;
            Unsafe.CopyBlock(pinnedDestination, pinnedSource, (uint)this.Count);
        }

        [Benchmark(Description = "Unsafe.CopyBlockUnaligned(ref)")]
        public void UnsafeCopyBlockUnalignedReferences()
        {
            Unsafe.CopyBlockUnaligned(ref this.destArray[0], ref this.sourceArray[0], (uint)this.Count);
        }

        [Benchmark(Description = "Unsafe.CopyBlockUnaligned(ptr)")]
        public unsafe void UnsafeCopyBlockUnalignedPointers()
        {
            void* pinnedDestination = this.destHandle.Pointer;
            void* pinnedSource = this.sourceHandle.Pointer;
            Unsafe.CopyBlockUnaligned(pinnedDestination, pinnedSource, (uint)this.Count);
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
        // |                         Method |  Job | Runtime | Count |       Mean |      Error |    StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
        // |------------------------------- |----- |-------- |------ |-----------:|-----------:|----------:|------:|--------:|------:|------:|------:|----------:|
        // |                   Array.Copy() |  Clr |     Clr |    10 |  23.636 ns |  2.5299 ns | 0.1387 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() |  Clr |     Clr |    10 |  11.420 ns |  2.3341 ns | 0.1279 ns |  0.48 |    0.01 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() |  Clr |     Clr |    10 |   2.861 ns |  0.5059 ns | 0.0277 ns |  0.12 |    0.00 |     - |     - |     - |         - |
        // |                 Marshal.Copy() |  Clr |     Clr |    10 |  14.870 ns |  2.4541 ns | 0.1345 ns |  0.63 |    0.01 |     - |     - |     - |         - |
        // |                  Span.CopyTo() |  Clr |     Clr |    10 |  31.906 ns |  1.2213 ns | 0.0669 ns |  1.35 |    0.01 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) |  Clr |     Clr |    10 |   3.513 ns |  0.7392 ns | 0.0405 ns |  0.15 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) |  Clr |     Clr |    10 |   3.053 ns |  0.2010 ns | 0.0110 ns |  0.13 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) |  Clr |     Clr |    10 |   3.497 ns |  0.4911 ns | 0.0269 ns |  0.15 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) |  Clr |     Clr |    10 |   3.109 ns |  0.5958 ns | 0.0327 ns |  0.13 |    0.00 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() | Core |    Core |    10 |  19.709 ns |  2.1867 ns | 0.1199 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() | Core |    Core |    10 |   7.377 ns |  1.1582 ns | 0.0635 ns |  0.37 |    0.01 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() | Core |    Core |    10 |   2.581 ns |  1.1607 ns | 0.0636 ns |  0.13 |    0.00 |     - |     - |     - |         - |
        // |                 Marshal.Copy() | Core |    Core |    10 |  15.197 ns |  2.8446 ns | 0.1559 ns |  0.77 |    0.01 |     - |     - |     - |         - |
        // |                  Span.CopyTo() | Core |    Core |    10 |  25.394 ns |  0.9782 ns | 0.0536 ns |  1.29 |    0.01 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) | Core |    Core |    10 |   2.254 ns |  0.1590 ns | 0.0087 ns |  0.11 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) | Core |    Core |    10 |   1.878 ns |  0.1035 ns | 0.0057 ns |  0.10 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) | Core |    Core |    10 |   2.263 ns |  0.1383 ns | 0.0076 ns |  0.11 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) | Core |    Core |    10 |   1.877 ns |  0.0602 ns | 0.0033 ns |  0.10 |    0.00 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() |  Clr |     Clr |    50 |  35.068 ns |  5.9137 ns | 0.3242 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() |  Clr |     Clr |    50 |  23.299 ns |  2.3797 ns | 0.1304 ns |  0.66 |    0.01 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() |  Clr |     Clr |    50 |   3.598 ns |  4.8536 ns | 0.2660 ns |  0.10 |    0.01 |     - |     - |     - |         - |
        // |                 Marshal.Copy() |  Clr |     Clr |    50 |  27.720 ns |  4.6602 ns | 0.2554 ns |  0.79 |    0.01 |     - |     - |     - |         - |
        // |                  Span.CopyTo() |  Clr |     Clr |    50 |  35.673 ns | 16.2972 ns | 0.8933 ns |  1.02 |    0.03 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) |  Clr |     Clr |    50 |   5.534 ns |  2.8119 ns | 0.1541 ns |  0.16 |    0.01 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) |  Clr |     Clr |    50 |   4.511 ns |  0.9555 ns | 0.0524 ns |  0.13 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) |  Clr |     Clr |    50 |   5.613 ns |  1.6679 ns | 0.0914 ns |  0.16 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) |  Clr |     Clr |    50 |   4.884 ns |  7.3153 ns | 0.4010 ns |  0.14 |    0.01 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() | Core |    Core |    50 |  20.232 ns |  1.5720 ns | 0.0862 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() | Core |    Core |    50 |   8.142 ns |  0.7860 ns | 0.0431 ns |  0.40 |    0.00 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() | Core |    Core |    50 |   2.962 ns |  0.0611 ns | 0.0033 ns |  0.15 |    0.00 |     - |     - |     - |         - |
        // |                 Marshal.Copy() | Core |    Core |    50 |  16.802 ns |  2.9686 ns | 0.1627 ns |  0.83 |    0.00 |     - |     - |     - |         - |
        // |                  Span.CopyTo() | Core |    Core |    50 |  26.571 ns |  0.9228 ns | 0.0506 ns |  1.31 |    0.01 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) | Core |    Core |    50 |   2.219 ns |  0.7191 ns | 0.0394 ns |  0.11 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) | Core |    Core |    50 |   1.751 ns |  0.1884 ns | 0.0103 ns |  0.09 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) | Core |    Core |    50 |   2.177 ns |  0.4489 ns | 0.0246 ns |  0.11 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) | Core |    Core |    50 |   1.806 ns |  0.1063 ns | 0.0058 ns |  0.09 |    0.00 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() |  Clr |     Clr |   100 |  39.158 ns |  4.3068 ns | 0.2361 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() |  Clr |     Clr |   100 |  27.623 ns |  0.4611 ns | 0.0253 ns |  0.71 |    0.00 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() |  Clr |     Clr |   100 |   5.018 ns |  0.5689 ns | 0.0312 ns |  0.13 |    0.00 |     - |     - |     - |         - |
        // |                 Marshal.Copy() |  Clr |     Clr |   100 |  33.527 ns |  1.9019 ns | 0.1042 ns |  0.86 |    0.01 |     - |     - |     - |         - |
        // |                  Span.CopyTo() |  Clr |     Clr |   100 |  35.604 ns |  2.7039 ns | 0.1482 ns |  0.91 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) |  Clr |     Clr |   100 |   7.853 ns |  0.4925 ns | 0.0270 ns |  0.20 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) |  Clr |     Clr |   100 |   7.406 ns |  1.9733 ns | 0.1082 ns |  0.19 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) |  Clr |     Clr |   100 |   7.822 ns |  0.6837 ns | 0.0375 ns |  0.20 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) |  Clr |     Clr |   100 |   7.392 ns |  1.2832 ns | 0.0703 ns |  0.19 |    0.00 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() | Core |    Core |   100 |  22.909 ns |  2.9754 ns | 0.1631 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() | Core |    Core |   100 |  10.687 ns |  1.1262 ns | 0.0617 ns |  0.47 |    0.00 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() | Core |    Core |   100 |   4.063 ns |  0.1607 ns | 0.0088 ns |  0.18 |    0.00 |     - |     - |     - |         - |
        // |                 Marshal.Copy() | Core |    Core |   100 |  18.067 ns |  4.0557 ns | 0.2223 ns |  0.79 |    0.01 |     - |     - |     - |         - |
        // |                  Span.CopyTo() | Core |    Core |   100 |  28.352 ns |  1.2762 ns | 0.0700 ns |  1.24 |    0.01 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) | Core |    Core |   100 |   4.130 ns |  0.2013 ns | 0.0110 ns |  0.18 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) | Core |    Core |   100 |   4.096 ns |  0.2460 ns | 0.0135 ns |  0.18 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) | Core |    Core |   100 |   4.160 ns |  0.3174 ns | 0.0174 ns |  0.18 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) | Core |    Core |   100 |   3.480 ns |  1.1683 ns | 0.0640 ns |  0.15 |    0.00 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() |  Clr |     Clr |  1000 |  49.059 ns |  2.0729 ns | 0.1136 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() |  Clr |     Clr |  1000 |  38.270 ns | 23.6970 ns | 1.2989 ns |  0.78 |    0.03 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() |  Clr |     Clr |  1000 |  27.599 ns |  6.8328 ns | 0.3745 ns |  0.56 |    0.01 |     - |     - |     - |         - |
        // |                 Marshal.Copy() |  Clr |     Clr |  1000 |  42.752 ns |  5.1357 ns | 0.2815 ns |  0.87 |    0.01 |     - |     - |     - |         - |
        // |                  Span.CopyTo() |  Clr |     Clr |  1000 |  69.983 ns |  2.1860 ns | 0.1198 ns |  1.43 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) |  Clr |     Clr |  1000 |  44.822 ns |  0.1625 ns | 0.0089 ns |  0.91 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) |  Clr |     Clr |  1000 |  45.072 ns |  1.4053 ns | 0.0770 ns |  0.92 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) |  Clr |     Clr |  1000 |  45.306 ns |  5.2646 ns | 0.2886 ns |  0.92 |    0.01 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) |  Clr |     Clr |  1000 |  44.813 ns |  0.9117 ns | 0.0500 ns |  0.91 |    0.00 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() | Core |    Core |  1000 |  51.907 ns |  3.1827 ns | 0.1745 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() | Core |    Core |  1000 |  40.700 ns |  3.1488 ns | 0.1726 ns |  0.78 |    0.00 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() | Core |    Core |  1000 |  23.711 ns |  1.3004 ns | 0.0713 ns |  0.46 |    0.00 |     - |     - |     - |         - |
        // |                 Marshal.Copy() | Core |    Core |  1000 |  42.586 ns |  2.5390 ns | 0.1392 ns |  0.82 |    0.00 |     - |     - |     - |         - |
        // |                  Span.CopyTo() | Core |    Core |  1000 |  44.109 ns |  4.5604 ns | 0.2500 ns |  0.85 |    0.01 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) | Core |    Core |  1000 |  33.926 ns |  5.1633 ns | 0.2830 ns |  0.65 |    0.01 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) | Core |    Core |  1000 |  33.267 ns |  0.2708 ns | 0.0148 ns |  0.64 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) | Core |    Core |  1000 |  34.018 ns |  2.3238 ns | 0.1274 ns |  0.66 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) | Core |    Core |  1000 |  33.667 ns |  2.1983 ns | 0.1205 ns |  0.65 |    0.00 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() |  Clr |     Clr | 10000 | 153.429 ns |  6.1735 ns | 0.3384 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() |  Clr |     Clr | 10000 | 201.373 ns |  4.3670 ns | 0.2394 ns |  1.31 |    0.00 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() |  Clr |     Clr | 10000 | 211.768 ns | 71.3510 ns | 3.9110 ns |  1.38 |    0.02 |     - |     - |     - |         - |
        // |                 Marshal.Copy() |  Clr |     Clr | 10000 | 215.299 ns | 17.2677 ns | 0.9465 ns |  1.40 |    0.01 |     - |     - |     - |         - |
        // |                  Span.CopyTo() |  Clr |     Clr | 10000 | 486.325 ns | 32.4445 ns | 1.7784 ns |  3.17 |    0.01 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) |  Clr |     Clr | 10000 | 452.314 ns | 33.0593 ns | 1.8121 ns |  2.95 |    0.02 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) |  Clr |     Clr | 10000 | 455.600 ns | 56.7534 ns | 3.1108 ns |  2.97 |    0.02 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) |  Clr |     Clr | 10000 | 452.279 ns |  8.6457 ns | 0.4739 ns |  2.95 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) |  Clr |     Clr | 10000 | 453.146 ns | 12.3776 ns | 0.6785 ns |  2.95 |    0.00 |     - |     - |     - |         - |
        // |                                |      |         |       |            |            |           |       |         |       |       |       |           |
        // |                   Array.Copy() | Core |    Core | 10000 | 204.508 ns |  3.1652 ns | 0.1735 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |             Buffer.BlockCopy() | Core |    Core | 10000 | 193.345 ns |  1.3742 ns | 0.0753 ns |  0.95 |    0.00 |     - |     - |     - |         - |
        // |            Buffer.MemoryCopy() | Core |    Core | 10000 | 196.978 ns | 18.3279 ns | 1.0046 ns |  0.96 |    0.01 |     - |     - |     - |         - |
        // |                 Marshal.Copy() | Core |    Core | 10000 | 206.878 ns |  6.9938 ns | 0.3834 ns |  1.01 |    0.00 |     - |     - |     - |         - |
        // |                  Span.CopyTo() | Core |    Core | 10000 | 215.733 ns | 15.4824 ns | 0.8486 ns |  1.05 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ref) | Core |    Core | 10000 | 186.894 ns |  8.7617 ns | 0.4803 ns |  0.91 |    0.00 |     - |     - |     - |         - |
        // |          Unsafe.CopyBlock(ptr) | Core |    Core | 10000 | 186.662 ns | 10.6059 ns | 0.5813 ns |  0.91 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ref) | Core |    Core | 10000 | 187.489 ns | 13.1527 ns | 0.7209 ns |  0.92 |    0.00 |     - |     - |     - |         - |
        // | Unsafe.CopyBlockUnaligned(ptr) | Core |    Core | 10000 | 186.586 ns |  4.6274 ns | 0.2536 ns |  0.91 |    0.00 |     - |     - |     - |         - |
    }
}
