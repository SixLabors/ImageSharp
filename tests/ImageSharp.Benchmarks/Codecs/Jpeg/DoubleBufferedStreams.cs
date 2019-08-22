// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class DoubleBufferedStreams
    {
        private readonly byte[] buffer = CreateTestBytes();
        private readonly byte[] chunk1 = new byte[2];
        private readonly byte[] chunk2 = new byte[2];

        private MemoryStream stream1;
        private MemoryStream stream2;
        private MemoryStream stream3;
        private MemoryStream stream4;
        private DoubleBufferedStreamReader reader1;
        private DoubleBufferedStreamReader reader2;

        [GlobalSetup]
        public void CreateStreams()
        {
            this.stream1 = new MemoryStream(this.buffer);
            this.stream2 = new MemoryStream(this.buffer);
            this.stream3 = new MemoryStream(this.buffer);
            this.stream4 = new MemoryStream(this.buffer);
            this.reader1 = new DoubleBufferedStreamReader(Configuration.Default.MemoryAllocator, this.stream2);
            this.reader2 = new DoubleBufferedStreamReader(Configuration.Default.MemoryAllocator, this.stream2);
        }

        [GlobalCleanup]
        public void DestroyStreams()
        {
            this.stream1?.Dispose();
            this.stream2?.Dispose();
            this.stream3?.Dispose();
            this.stream4?.Dispose();
            this.reader1?.Dispose();
            this.reader2?.Dispose();
        }

        [Benchmark(Baseline = true)]
        public int StandardStreamReadByte()
        {
            int r = 0;
            Stream stream = this.stream1;

            for (int i = 0; i < stream.Length; i++)
            {
                r += stream.ReadByte();
            }

            return r;
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
        public int DoubleBufferedStreamReadByte()
        {
            int r = 0;
            DoubleBufferedStreamReader reader = this.reader1;

            for (int i = 0; i < reader.Length; i++)
            {
                r += reader.ReadByte();
            }

            return r;
        }

        [Benchmark]
        public int DoubleBufferedStreamRead()
        {
            int r = 0;
            DoubleBufferedStreamReader reader = this.reader2;
            byte[] b = this.chunk2;

            for (int i = 0; i < reader.Length / 2; i++)
            {
                r += reader.Read(b, 0, 2);
            }

            return r;
        }

        [Benchmark]
        public int SimpleReadByte()
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
            var buffer = new byte[DoubleBufferedStreamReader.ChunkLength * 3];
            var random = new Random();
            random.NextBytes(buffer);

            return buffer;
        }
    }

    // RESULTS (2019 April 24):
    //
    //BenchmarkDotNet=v0.11.5, OS=Windows 10.0.17763.437 (1809/October2018Update/Redstone5)
    //Intel Core i7-6600U CPU 2.60GHz (Skylake), 1 CPU, 4 logical and 2 physical cores
    //.NET Core SDK=2.2.202
    //  [Host] : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
    //  Clr    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3362.0
    //  Core   : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
    //
    //IterationCount=3  LaunchCount=1  WarmupCount=3
    //
    //|                       Method |  Job | Runtime |     Mean |      Error |    StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    //|----------------------------- |----- |-------- |---------:|-----------:|----------:|------:|--------:|------:|------:|------:|----------:|
    //|       StandardStreamReadByte |  Clr |     Clr | 96.71 us |  5.9950 us | 0.3286 us |  1.00 |    0.00 |     - |     - |     - |         - |
    //|           StandardStreamRead |  Clr |     Clr | 77.73 us |  5.2284 us | 0.2866 us |  0.80 |    0.00 |     - |     - |     - |         - |
    //| DoubleBufferedStreamReadByte |  Clr |     Clr | 23.17 us | 26.2354 us | 1.4381 us |  0.24 |    0.01 |     - |     - |     - |         - |
    //|     DoubleBufferedStreamRead |  Clr |     Clr | 33.35 us |  3.4071 us | 0.1868 us |  0.34 |    0.00 |     - |     - |     - |         - |
    //|               SimpleReadByte |  Clr |     Clr | 10.85 us |  0.4927 us | 0.0270 us |  0.11 |    0.00 |     - |     - |     - |         - |
    //|                              |      |         |          |            |           |       |         |       |       |       |           |
    //|       StandardStreamReadByte | Core |    Core | 75.35 us | 12.9789 us | 0.7114 us |  1.00 |    0.00 |     - |     - |     - |         - |
    //|           StandardStreamRead | Core |    Core | 55.36 us |  1.4432 us | 0.0791 us |  0.73 |    0.01 |     - |     - |     - |         - |
    //| DoubleBufferedStreamReadByte | Core |    Core | 21.47 us | 29.7076 us | 1.6284 us |  0.28 |    0.02 |     - |     - |     - |         - |
    //|     DoubleBufferedStreamRead | Core |    Core | 29.67 us |  2.5988 us | 0.1424 us |  0.39 |    0.00 |     - |     - |     - |         - |
    //|               SimpleReadByte | Core |    Core | 10.84 us |  0.7567 us | 0.0415 us |  0.14 |    0.00 |     - |     - |     - |         - |
}
