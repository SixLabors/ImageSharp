using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    [Config(typeof(Config.ShortClr))]
    public class DoubleBufferedStreams
    {
        private byte[] buffer = CreateTestBytes();

        [Benchmark]
        public int StandardStream()
        {
            int r = 0;
            using (var stream = new MemoryStream(this.buffer))
            {
                for (int i = 0; i < stream.Length; i++)
                {
                    r += stream.ReadByte();
                }
            }

            return r;
        }

        [Benchmark]
        public int ChunkedStream()
        {
            int r = 0;
            using (var stream = new MemoryStream(this.buffer))
            {
                var reader = new DoubleBufferedStreamReader(stream);
                for (int i = 0; i < reader.Length; i++)
                {
                    r += reader.ReadByte();
                }
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
