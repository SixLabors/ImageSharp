// ReSharper disable InconsistentNaming
namespace ImageSharp.Benchmarks.General
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using BenchmarkDotNet.Attributes;

    using Color = ImageSharp.Color;

    public unsafe class ClearBuffer
    {
        private PinnedBuffer<Color> buffer;
        
        [Params(32, 128, 512)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.buffer = new PinnedBuffer<ImageSharp.Color>(this.Count);
        }

        [Cleanup]
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
            Unsafe.InitBlock((void*)this.buffer.Pointer, default(byte), (uint)this.Count*sizeof(uint));
        }
    }
}