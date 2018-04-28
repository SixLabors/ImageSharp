// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class DoubleBufferedStreams
    {
        private byte[] buffer = CreateTestBytes();

        private MemoryStream stream1;
        private MemoryStream stream2;
        DoubleBufferedStreamReader reader;

        [GlobalSetup]
        public void CreateStreams()
        {
            this.stream1 = new MemoryStream(this.buffer);
            this.stream2 = new MemoryStream(this.buffer);
            this.reader = new DoubleBufferedStreamReader(Configuration.Default.MemoryManager, this.stream2);
        }

        [GlobalCleanup]
        public void DestroyStreams()
        {
            this.stream1?.Dispose();
            this.stream2?.Dispose();
            this.reader?.Dispose();
        }

        [Benchmark(Baseline = true)]
        public int StandardStream()
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
        public int DoubleBufferedStream()
        {
            int r = 0;
            DoubleBufferedStreamReader reader = this.reader;

            for (int i = 0; i < reader.Length; i++)
            {
                r += reader.ReadByte();
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
