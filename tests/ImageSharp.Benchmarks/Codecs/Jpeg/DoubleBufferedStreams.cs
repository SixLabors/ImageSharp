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
        DoubleBufferedStreamReader reader1;
        DoubleBufferedStreamReader reader2;

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

        private static byte[] CreateTestBytes()
        {
            byte[] buffer = new byte[DoubleBufferedStreamReader.ChunkLength * 3];
            var random = new Random();
            random.NextBytes(buffer);

            return buffer;
        }
    }
}
