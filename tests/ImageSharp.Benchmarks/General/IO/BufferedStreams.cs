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
            this.bufferedStream1 = new BufferedReadStream(Configuration.Default, this.stream3);
            this.bufferedStream2 = new BufferedReadStream(Configuration.Default, this.stream4);
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
            var buffer = new byte[Configuration.Default.StreamProcessingBufferSize * 3];
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

|                         Method |       Runtime |      Mean |       Error |     StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
|------------------------------- |-------------- |----------:|------------:|-----------:|------:|--------:|------:|------:|------:|----------:|
|             StandardStreamRead |    .NET 4.7.2 | 63.238 us |  49.7827 us |  2.7288 us |  0.66 |    0.13 |     - |     - |     - |         - |
|         BufferedReadStreamRead |    .NET 4.7.2 | 66.092 us |   0.4273 us |  0.0234 us |  0.69 |    0.11 |     - |     - |     - |         - |
|     BufferedReadStreamWrapRead |    .NET 4.7.2 | 26.216 us |   3.0527 us |  0.1673 us |  0.27 |    0.04 |     - |     - |     - |         - |
|         StandardStreamReadByte |    .NET 4.7.2 | 97.900 us | 261.7204 us | 14.3458 us |  1.00 |    0.00 |     - |     - |     - |         - |
|     BufferedReadStreamReadByte |    .NET 4.7.2 | 97.260 us |   1.2979 us |  0.0711 us |  1.01 |    0.15 |     - |     - |     - |         - |
| BufferedReadStreamWrapReadByte |    .NET 4.7.2 | 19.170 us |   2.2296 us |  0.1222 us |  0.20 |    0.03 |     - |     - |     - |         - |
|                  ArrayReadByte |    .NET 4.7.2 | 12.878 us |  11.1292 us |  0.6100 us |  0.13 |    0.02 |     - |     - |     - |         - |
|                                |               |           |             |            |       |         |       |       |       |           |
|             StandardStreamRead | .NET Core 2.1 | 60.618 us | 131.7038 us |  7.2191 us |  0.78 |    0.10 |     - |     - |     - |         - |
|         BufferedReadStreamRead | .NET Core 2.1 | 30.006 us |  25.2499 us |  1.3840 us |  0.38 |    0.02 |     - |     - |     - |         - |
|     BufferedReadStreamWrapRead | .NET Core 2.1 | 29.241 us |   6.5020 us |  0.3564 us |  0.37 |    0.01 |     - |     - |     - |         - |
|         StandardStreamReadByte | .NET Core 2.1 | 78.074 us |  15.8463 us |  0.8686 us |  1.00 |    0.00 |     - |     - |     - |         - |
|     BufferedReadStreamReadByte | .NET Core 2.1 | 14.737 us |  20.1510 us |  1.1045 us |  0.19 |    0.01 |     - |     - |     - |         - |
| BufferedReadStreamWrapReadByte | .NET Core 2.1 | 13.234 us |   1.4711 us |  0.0806 us |  0.17 |    0.00 |     - |     - |     - |         - |
|                  ArrayReadByte | .NET Core 2.1 |  9.373 us |   0.6108 us |  0.0335 us |  0.12 |    0.00 |     - |     - |     - |         - |
|                                |               |           |             |            |       |         |       |       |       |           |
|             StandardStreamRead | .NET Core 3.1 | 52.151 us |  19.9456 us |  1.0933 us |  0.65 |    0.03 |     - |     - |     - |         - |
|         BufferedReadStreamRead | .NET Core 3.1 | 29.217 us |   0.2490 us |  0.0136 us |  0.36 |    0.01 |     - |     - |     - |         - |
|     BufferedReadStreamWrapRead | .NET Core 3.1 | 32.962 us |   7.1382 us |  0.3913 us |  0.41 |    0.02 |     - |     - |     - |         - |
|         StandardStreamReadByte | .NET Core 3.1 | 80.310 us |  45.0350 us |  2.4685 us |  1.00 |    0.00 |     - |     - |     - |         - |
|     BufferedReadStreamReadByte | .NET Core 3.1 | 13.092 us |   0.6268 us |  0.0344 us |  0.16 |    0.00 |     - |     - |     - |         - |
| BufferedReadStreamWrapReadByte | .NET Core 3.1 | 13.282 us |   3.8689 us |  0.2121 us |  0.17 |    0.01 |     - |     - |     - |         - |
|                  ArrayReadByte | .NET Core 3.1 |  9.349 us |   2.9860 us |  0.1637 us |  0.12 |    0.00 |     - |     - |     - |         - |
    */
}
