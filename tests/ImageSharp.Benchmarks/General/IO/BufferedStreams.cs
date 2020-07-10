// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Benchmarks.IO
{
    [Config(typeof(Config.ShortClr))]
    public class BufferedStreams
    {
        private readonly byte[] buffer = CreateTestBytes();
        private readonly byte[] chunk1 = new byte[2];
        private readonly byte[] chunk2 = new byte[2];

        private MemoryStream stream1;
        private MemoryStream stream2;
        private MemoryStream stream3;
        private MemoryStream stream4;
        private MemoryStream stream5;
        private MemoryStream stream6;
        private BufferedReadStream bufferedStream1;
        private BufferedReadStream bufferedStream2;
        private BufferedReadStreamWrapper bufferedStreamWrap1;
        private BufferedReadStreamWrapper bufferedStreamWrap2;

        [GlobalSetup]
        public void CreateStreams()
        {
            this.stream1 = new MemoryStream(this.buffer);
            this.stream2 = new MemoryStream(this.buffer);
            this.stream3 = new MemoryStream(this.buffer);
            this.stream4 = new MemoryStream(this.buffer);
            this.stream5 = new MemoryStream(this.buffer);
            this.stream6 = new MemoryStream(this.buffer);
            this.bufferedStream1 = new BufferedReadStream(this.stream3);
            this.bufferedStream2 = new BufferedReadStream(this.stream4);
            this.bufferedStreamWrap1 = new BufferedReadStreamWrapper(this.stream5);
            this.bufferedStreamWrap2 = new BufferedReadStreamWrapper(this.stream6);
        }

        [GlobalCleanup]
        public void DestroyStreams()
        {
            this.bufferedStream1?.Dispose();
            this.bufferedStream2?.Dispose();
            this.bufferedStreamWrap1?.Dispose();
            this.bufferedStreamWrap2?.Dispose();
            this.stream1?.Dispose();
            this.stream2?.Dispose();
            this.stream3?.Dispose();
            this.stream4?.Dispose();
            this.stream5?.Dispose();
            this.stream6?.Dispose();
        }

        [Benchmark]
        public int StandardStreamRead()
        {
            int r = 0;
            Stream stream = this.stream1;
            byte[] b = this.chunk1;

            for (int i = 0; i < stream.Length / 2; i++)
            {
                r += stream.Read(b, 0, 2);
            }

            return r;
        }

        [Benchmark]
        public int BufferedReadStreamRead()
        {
            int r = 0;
            BufferedReadStream reader = this.bufferedStream1;
            byte[] b = this.chunk2;

            for (int i = 0; i < reader.Length / 2; i++)
            {
                r += reader.Read(b, 0, 2);
            }

            return r;
        }

        [Benchmark]
        public int BufferedReadStreamWrapRead()
        {
            int r = 0;
            BufferedReadStreamWrapper reader = this.bufferedStreamWrap1;
            byte[] b = this.chunk2;

            for (int i = 0; i < reader.Length / 2; i++)
            {
                r += reader.Read(b, 0, 2);
            }

            return r;
        }

        [Benchmark(Baseline = true)]
        public int StandardStreamReadByte()
        {
            int r = 0;
            Stream stream = this.stream2;

            for (int i = 0; i < stream.Length; i++)
            {
                r += stream.ReadByte();
            }

            return r;
        }

        [Benchmark]
        public int BufferedReadStreamReadByte()
        {
            int r = 0;
            BufferedReadStream reader = this.bufferedStream2;

            for (int i = 0; i < reader.Length; i++)
            {
                r += reader.ReadByte();
            }

            return r;
        }

        [Benchmark]
        public int BufferedReadStreamWrapReadByte()
        {
            int r = 0;
            BufferedReadStreamWrapper reader = this.bufferedStreamWrap2;

            for (int i = 0; i < reader.Length; i++)
            {
                r += reader.ReadByte();
            }

            return r;
        }

        [Benchmark]
        public int ArrayReadByte()
        {
            byte[] b = this.buffer;
            int r = 0;
            for (int i = 0; i < b.Length; i++)
            {
                r += b[i];
            }

            return r;
        }

        private static byte[] CreateTestBytes()
        {
            var buffer = new byte[BufferedReadStream.BufferLength * 3];
            var random = new Random();
            random.NextBytes(buffer);

            return buffer;
        }
    }

    /*
    BenchmarkDotNet=v0.12.0, OS=Windows 10.0.19041
    Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
    .NET Core SDK=3.1.301
      [Host]     : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT
      Job-LKLBOT : .NET Framework 4.8 (4.8.4180.0), X64 RyuJIT
      Job-RSTMKF : .NET Core 2.1.19 (CoreCLR 4.6.28928.01, CoreFX 4.6.28928.04), X64 RyuJIT
      Job-PZIHIV : .NET Core 3.1.5 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.27001), X64 RyuJIT

    IterationCount=3  LaunchCount=1  WarmupCount=3

    |                         Method |       Runtime |      Mean |      Error |    StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    |------------------------------- |-------------- |----------:|-----------:|----------:|------:|--------:|------:|------:|------:|----------:|
    |             StandardStreamRead |    .NET 4.7.2 | 126.07 us | 126.498 us |  6.934 us |  0.99 |    0.08 |     - |     - |     - |         - |
    |         BufferedReadStreamRead |    .NET 4.7.2 | 118.08 us |  42.234 us |  2.315 us |  0.92 |    0.03 |     - |     - |     - |         - |
    |     BufferedReadStreamWrapRead |    .NET 4.7.2 |  45.33 us |  22.833 us |  1.252 us |  0.35 |    0.00 |     - |     - |     - |         - |
    |         StandardStreamReadByte |    .NET 4.7.2 | 128.17 us |  94.616 us |  5.186 us |  1.00 |    0.00 |     - |     - |     - |         - |
    |     BufferedReadStreamReadByte |    .NET 4.7.2 | 143.60 us |  92.871 us |  5.091 us |  1.12 |    0.08 |     - |     - |     - |         - |
    | BufferedReadStreamWrapReadByte |    .NET 4.7.2 |  32.72 us |  53.708 us |  2.944 us |  0.26 |    0.02 |     - |     - |     - |         - |
    |                  ArrayReadByte |    .NET 4.7.2 |  19.40 us |  12.206 us |  0.669 us |  0.15 |    0.01 |     - |     - |     - |         - |
    |                                |               |           |            |           |       |         |       |       |       |           |
    |             StandardStreamRead | .NET Core 2.1 |  84.82 us |  55.983 us |  3.069 us |  0.75 |    0.15 |     - |     - |     - |         - |
    |         BufferedReadStreamRead | .NET Core 2.1 |  49.62 us |  27.253 us |  1.494 us |  0.44 |    0.08 |     - |     - |     - |         - |
    |     BufferedReadStreamWrapRead | .NET Core 2.1 |  67.78 us |  87.546 us |  4.799 us |  0.60 |    0.10 |     - |     - |     - |         - |
    |         StandardStreamReadByte | .NET Core 2.1 | 115.81 us | 382.107 us | 20.945 us |  1.00 |    0.00 |     - |     - |     - |         - |
    |     BufferedReadStreamReadByte | .NET Core 2.1 |  16.32 us |   6.123 us |  0.336 us |  0.14 |    0.02 |     - |     - |     - |         - |
    | BufferedReadStreamWrapReadByte | .NET Core 2.1 |  16.68 us |   4.616 us |  0.253 us |  0.15 |    0.03 |     - |     - |     - |         - |
    |                  ArrayReadByte | .NET Core 2.1 |  15.13 us |  60.763 us |  3.331 us |  0.14 |    0.05 |     - |     - |     - |         - |
    |                                |               |           |            |           |       |         |       |       |       |           |
    |             StandardStreamRead | .NET Core 3.1 |  92.44 us |  88.217 us |  4.835 us |  0.94 |    0.06 |     - |     - |     - |         - |
    |         BufferedReadStreamRead | .NET Core 3.1 |  36.41 us |   5.923 us |  0.325 us |  0.37 |    0.00 |     - |     - |     - |         - |
    |     BufferedReadStreamWrapRead | .NET Core 3.1 |  37.22 us |   9.628 us |  0.528 us |  0.38 |    0.01 |     - |     - |     - |         - |
    |         StandardStreamReadByte | .NET Core 3.1 |  98.67 us |  20.947 us |  1.148 us |  1.00 |    0.00 |     - |     - |     - |         - |
    |     BufferedReadStreamReadByte | .NET Core 3.1 |  41.36 us | 123.536 us |  6.771 us |  0.42 |    0.06 |     - |     - |     - |         - |
    | BufferedReadStreamWrapReadByte | .NET Core 3.1 |  39.11 us |  93.894 us |  5.147 us |  0.40 |    0.05 |     - |     - |     - |         - |
    |                  ArrayReadByte | .NET Core 3.1 |  18.84 us |   4.684 us |  0.257 us |  0.19 |    0.00 |     - |     - |     - |         - |
    */
}
