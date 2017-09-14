// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.General
{
    using System;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Memory;

    public unsafe class ClearBuffer
    {
        private Buffer<Rgba32> buffer;

        [Params(32, 128, 512)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.buffer = new Buffer<Rgba32>(this.Count);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.buffer.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void Array_Clear()
        {
            Array.Clear(this.buffer.Array, 0, this.Count);
        }

        [Benchmark]
        public void Unsafe_InitBlock()
        {
            Unsafe.InitBlock((void*)this.buffer.Pin(), default(byte), (uint)this.Count * sizeof(uint));
        }
    }
}