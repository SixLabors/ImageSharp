// <copyright file="ArrayCopy.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace SixLabors.ImageSharp.Benchmarks.General
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using BenchmarkDotNet.Attributes;
    
    [Config(typeof(Config.ShortClr))]
    public class ArrayCopy
    {
        [Params(10, 100, 1000, 10000)]
        public int Count { get; set; }

        byte[] source;

        byte[] destination;

        [GlobalSetup]
        public void SetUp()
        {
            this.source = new byte[this.Count];
            this.destination = new byte[this.Count];
        }

        [Benchmark(Baseline = true, Description = "Copy using Array.Copy()")]
        public void CopyArray()
        {
            Array.Copy(this.source, this.destination, this.Count);
        }

        [Benchmark(Description = "Copy using Unsafe<T>")]
        public unsafe void CopyUnsafe()
        {
            fixed (byte* pinnedDestination = this.destination)
            fixed (byte* pinnedSource = this.source)
            {
                Unsafe.CopyBlock(pinnedSource, pinnedDestination, (uint)this.Count);
            }
        }

        [Benchmark(Description = "Copy using Buffer.BlockCopy()")]
        public void CopyUsingBufferBlockCopy()
        {
            Buffer.BlockCopy(this.source, 0, this.destination, 0, this.Count);
        }

        [Benchmark(Description = "Copy using Buffer.MemoryCopy<T>")]
        public unsafe void CopyUsingBufferMemoryCopy()
        {
            fixed (byte* pinnedDestination = this.destination)
            fixed (byte* pinnedSource = this.source)
            {
                Buffer.MemoryCopy(pinnedSource, pinnedDestination, this.Count, this.Count);
            }
        }
        
        [Benchmark(Description = "Copy using Marshal.Copy<T>")]
        public unsafe void CopyUsingMarshalCopy()
        {
            fixed (byte* pinnedDestination = this.destination)
            {
                Marshal.Copy(this.source, 0, (IntPtr)pinnedDestination, this.Count);
            }
        }

        /*****************************************************************************************************************
         *************** RESULTS on i7-4810MQ 2.80GHz + Clr 4.0.30319.42000, 64bit RyuJIT-v4.6.1085.0 ********************
         *****************************************************************************************************************
         * 
         *                             Method | Count |        Mean |     StdErr |     StdDev | Scaled | Scaled-StdDev |
         * ---------------------------------- |------ |------------ |----------- |----------- |------- |-------------- |
         *          'Copy using Array.Copy()' |    10 |  20.3074 ns |  0.1194 ns |  0.2068 ns |   1.00 |          0.00 |
         *             'Copy using Unsafe<T>' |    10 |   6.1002 ns |  0.1981 ns |  0.3432 ns |   0.30 |          0.01 |
         *    'Copy using Buffer.BlockCopy()' |    10 |  10.7879 ns |  0.0984 ns |  0.1705 ns |   0.53 |          0.01 |
         *  'Copy using Buffer.MemoryCopy<T>' |    10 |   4.9625 ns |  0.0200 ns |  0.0347 ns |   0.24 |          0.00 |
         *       'Copy using Marshal.Copy<T>' |    10 |  16.1782 ns |  0.0919 ns |  0.1592 ns |   0.80 |          0.01 |
         *       
         *          'Copy using Array.Copy()' |   100 |  31.5945 ns |  0.2908 ns |  0.5037 ns |   1.00 |          0.00 |
         *             'Copy using Unsafe<T>' |   100 |  10.2722 ns |  0.5202 ns |  0.9010 ns |   0.33 |          0.02 |
         *    'Copy using Buffer.BlockCopy()' |   100 |  22.0322 ns |  0.0284 ns |  0.0493 ns |   0.70 |          0.01 |
         *  'Copy using Buffer.MemoryCopy<T>' |   100 |  10.2472 ns |  0.0359 ns |  0.0622 ns |   0.32 |          0.00 |
         *       'Copy using Marshal.Copy<T>' |   100 |  34.3820 ns |  1.1868 ns |  2.0555 ns |   1.09 |          0.05 |
         *       
         *          'Copy using Array.Copy()' |  1000 |  40.9743 ns |  0.0521 ns |  0.0902 ns |   1.00 |          0.00 |
         *             'Copy using Unsafe<T>' |  1000 |  42.7840 ns |  2.0139 ns |  3.4882 ns |   1.04 |          0.07 |
         *    'Copy using Buffer.BlockCopy()' |  1000 |  33.7361 ns |  0.0751 ns |  0.1300 ns |   0.82 |          0.00 |
         *  'Copy using Buffer.MemoryCopy<T>' |  1000 |  35.7541 ns |  0.0480 ns |  0.0832 ns |   0.87 |          0.00 |
         *       'Copy using Marshal.Copy<T>' |  1000 |  42.2028 ns |  0.2769 ns |  0.4795 ns |   1.03 |          0.01 |
         *       
         *          'Copy using Array.Copy()' | 10000 | 200.0438 ns |  0.2251 ns |  0.3899 ns |   1.00 |          0.00 |
         *             'Copy using Unsafe<T>' | 10000 | 389.6957 ns | 13.2770 ns | 22.9964 ns |   1.95 |          0.09 |
         *    'Copy using Buffer.BlockCopy()' | 10000 | 191.3478 ns |  0.1557 ns |  0.2697 ns |   0.96 |          0.00 |
         *  'Copy using Buffer.MemoryCopy<T>' | 10000 | 196.4679 ns |  0.2731 ns |  0.4730 ns |   0.98 |          0.00 |
         *       'Copy using Marshal.Copy<T>' | 10000 | 202.5392 ns |  0.5561 ns |  0.9631 ns |   1.01 |          0.00 |
         * 
         */
    }
}
