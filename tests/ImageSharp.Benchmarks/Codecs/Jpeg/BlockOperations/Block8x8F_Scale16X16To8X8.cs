using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Format.Jpeg.Components
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class Block8x8F_Scale16X16To8X8
    {
        private Block8x8F source;
        private readonly Block8x8F[] target = new Block8x8F[4];

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random();

            float[] f = new float[8*8];
            for (int i = 0; i < f.Length; i++)
            {
                f[i] = (float)random.NextDouble();
            }

            for (int i = 0; i < 4; i++)
            {
                this.target[i] = Block8x8F.Load(f);
            }

            this.source = Block8x8F.Load(f);
        }

        [Benchmark]
        public void Scale16X16To8X8() => Block8x8F.Scale16X16To8X8(ref this.source, this.target);
    }
}
