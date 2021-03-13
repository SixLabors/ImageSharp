// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Benchmarks.IO
{
    [Config(typeof(Config.ShortMultiFramework))]
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
        private ChunkedMemoryStream chunkedMemoryStream1;
        private ChunkedMemoryStream chunkedMemoryStream2;
        private BufferedReadStream bufferedStream1;
        private BufferedReadStream bufferedStream2;
        private BufferedReadStream bufferedStream3;
        private BufferedReadStream bufferedStream4;
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
            this.stream6 = new MemoryStream(this.buffer);

            this.chunkedMemoryStream1 = new ChunkedMemoryStream(Configuration.Default.MemoryAllocator);
            this.chunkedMemoryStream1.Write(this.buffer);
            this.chunkedMemoryStream1.Position = 0;

            this.chunkedMemoryStream2 = new ChunkedMemoryStream(Configuration.Default.MemoryAllocator);
            this.chunkedMemoryStream2.Write(this.buffer);
            this.chunkedMemoryStream2.Position = 0;

            this.bufferedStream1 = new BufferedReadStream(Configuration.Default, this.stream3);
            this.bufferedStream2 = new BufferedReadStream(Configuration.Default, this.stream4);
            this.bufferedStream3 = new BufferedReadStream(Configuration.Default, this.chunkedMemoryStream1);
            this.bufferedStream4 = new BufferedReadStream(Configuration.Default, this.chunkedMemoryStream2);
            this.bufferedStreamWrap1 = new BufferedReadStreamWrapper(this.stream5);
            this.bufferedStreamWrap2 = new BufferedReadStreamWrapper(this.stream6);
        }

        [GlobalCleanup]
        public void DestroyStreams()
        {
            this.bufferedStream1?.Dispose();
            this.bufferedStream2?.Dispose();
            this.bufferedStream3?.Dispose();
            this.bufferedStream4?.Dispose();
            this.bufferedStreamWrap1?.Dispose();
            this.bufferedStreamWrap2?.Dispose();
            this.chunkedMemoryStream1?.Dispose();
            this.chunkedMemoryStream2?.Dispose();
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
        public int BufferedReadStreamChunkedRead()
        {
            int r = 0;
            BufferedReadStream reader = this.bufferedStream3;
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
        public int BufferedReadStreamChunkedReadByte()
        {
            int r = 0;
            BufferedReadStream reader = this.bufferedStream4;

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
    BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.450 (2004/?/20H1)
    Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
    .NET Core SDK=3.1.401
      [Host]     : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT
      Job-OKZLUV : .NET Framework 4.8 (4.8.4084.0), X64 RyuJIT
      Job-CPYMXV : .NET Core 2.1.21 (CoreCLR 4.6.29130.01, CoreFX 4.6.29130.02), X64 RyuJIT
      Job-BSGVGU : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT

    IterationCount=3  LaunchCount=1  WarmupCount=3

    |                            Method |        Job |       Runtime |       Mean |     Error |    StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    |---------------------------------- |----------- |-------------- |-----------:|----------:|----------:|------:|--------:|------:|------:|------:|----------:|
    |                StandardStreamRead | Job-OKZLUV |    .NET 4.7.2 |  66.785 us | 15.768 us | 0.8643 us |  0.83 |    0.01 |     - |     - |     - |         - |
    |            BufferedReadStreamRead | Job-OKZLUV |    .NET 4.7.2 |  97.389 us | 17.658 us | 0.9679 us |  1.21 |    0.01 |     - |     - |     - |         - |
    |     BufferedReadStreamChunkedRead | Job-OKZLUV |    .NET 4.7.2 |  96.006 us | 16.286 us | 0.8927 us |  1.20 |    0.02 |     - |     - |     - |         - |
    |        BufferedReadStreamWrapRead | Job-OKZLUV |    .NET 4.7.2 |  37.064 us | 14.640 us | 0.8024 us |  0.46 |    0.02 |     - |     - |     - |         - |
    |            StandardStreamReadByte | Job-OKZLUV |    .NET 4.7.2 |  80.315 us | 26.676 us | 1.4622 us |  1.00 |    0.00 |     - |     - |     - |         - |
    |        BufferedReadStreamReadByte | Job-OKZLUV |    .NET 4.7.2 | 118.706 us | 38.013 us | 2.0836 us |  1.48 |    0.00 |     - |     - |     - |         - |
    | BufferedReadStreamChunkedReadByte | Job-OKZLUV |    .NET 4.7.2 | 115.437 us | 33.352 us | 1.8282 us |  1.44 |    0.01 |     - |     - |     - |         - |
    |    BufferedReadStreamWrapReadByte | Job-OKZLUV |    .NET 4.7.2 |  16.449 us | 11.400 us | 0.6249 us |  0.20 |    0.00 |     - |     - |     - |         - |
    |                     ArrayReadByte | Job-OKZLUV |    .NET 4.7.2 |  10.416 us |  1.866 us | 0.1023 us |  0.13 |    0.00 |     - |     - |     - |         - |
    |                                   |            |               |            |           |           |       |         |       |       |       |           |
    |                StandardStreamRead | Job-CPYMXV | .NET Core 2.1 |  71.425 us | 50.441 us | 2.7648 us |  0.82 |    0.03 |     - |     - |     - |         - |
    |            BufferedReadStreamRead | Job-CPYMXV | .NET Core 2.1 |  32.816 us |  6.655 us | 0.3648 us |  0.38 |    0.01 |     - |     - |     - |         - |
    |     BufferedReadStreamChunkedRead | Job-CPYMXV | .NET Core 2.1 |  31.995 us |  7.751 us | 0.4249 us |  0.37 |    0.01 |     - |     - |     - |         - |
    |        BufferedReadStreamWrapRead | Job-CPYMXV | .NET Core 2.1 |  31.970 us |  4.170 us | 0.2286 us |  0.37 |    0.01 |     - |     - |     - |         - |
    |            StandardStreamReadByte | Job-CPYMXV | .NET Core 2.1 |  86.909 us | 18.565 us | 1.0176 us |  1.00 |    0.00 |     - |     - |     - |         - |
    |        BufferedReadStreamReadByte | Job-CPYMXV | .NET Core 2.1 |  14.596 us | 10.889 us | 0.5969 us |  0.17 |    0.01 |     - |     - |     - |         - |
    | BufferedReadStreamChunkedReadByte | Job-CPYMXV | .NET Core 2.1 |  13.629 us |  1.569 us | 0.0860 us |  0.16 |    0.00 |     - |     - |     - |         - |
    |    BufferedReadStreamWrapReadByte | Job-CPYMXV | .NET Core 2.1 |  13.566 us |  1.743 us | 0.0956 us |  0.16 |    0.00 |     - |     - |     - |         - |
    |                     ArrayReadByte | Job-CPYMXV | .NET Core 2.1 |   9.771 us |  6.658 us | 0.3650 us |  0.11 |    0.00 |     - |     - |     - |         - |
    |                                   |            |               |            |           |           |       |         |       |       |       |           |
    |                StandardStreamRead | Job-BSGVGU | .NET Core 3.1 |  53.265 us | 65.819 us | 3.6078 us |  0.81 |    0.05 |     - |     - |     - |         - |
    |            BufferedReadStreamRead | Job-BSGVGU | .NET Core 3.1 |  33.163 us |  9.569 us | 0.5245 us |  0.51 |    0.01 |     - |     - |     - |         - |
    |     BufferedReadStreamChunkedRead | Job-BSGVGU | .NET Core 3.1 |  33.001 us |  6.114 us | 0.3351 us |  0.50 |    0.01 |     - |     - |     - |         - |
    |        BufferedReadStreamWrapRead | Job-BSGVGU | .NET Core 3.1 |  29.448 us |  7.120 us | 0.3902 us |  0.45 |    0.01 |     - |     - |     - |         - |
    |            StandardStreamReadByte | Job-BSGVGU | .NET Core 3.1 |  65.619 us |  6.732 us | 0.3690 us |  1.00 |    0.00 |     - |     - |     - |         - |
    |        BufferedReadStreamReadByte | Job-BSGVGU | .NET Core 3.1 |  13.989 us |  3.464 us | 0.1899 us |  0.21 |    0.00 |     - |     - |     - |         - |
    | BufferedReadStreamChunkedReadByte | Job-BSGVGU | .NET Core 3.1 |  13.806 us |  1.710 us | 0.0938 us |  0.21 |    0.00 |     - |     - |     - |         - |
    |    BufferedReadStreamWrapReadByte | Job-BSGVGU | .NET Core 3.1 |  13.690 us |  1.523 us | 0.0835 us |  0.21 |    0.00 |     - |     - |     - |         - |
    |                     ArrayReadByte | Job-BSGVGU | .NET Core 3.1 |  10.792 us |  8.228 us | 0.4510 us |  0.16 |    0.01 |     - |     - |     - |         - |
    */
}
