namespace SixLabors.ImageSharp.Benchmarks
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Memory;

    /// <summary>
    /// Compares two implementation candidates for general BulkPixelOperations.ToVector4():
    /// - One iterating with pointers
    /// - One iterating with ref locals
    /// </summary>
    public unsafe class PackFromVector4ReferenceVsPointer
    {
        private Buffer<Rgba32> destination;

        private Buffer<Vector4> source;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.destination = new Buffer<Rgba32>(this.Count);
            this.source = new Buffer<Vector4>(this.Count * 4);
            this.source.Pin();
            this.destination.Pin();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.source.Dispose();
            this.destination.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void PackUsingPointers()
        {
            Vector4* sp = (Vector4*)this.source.Pin();
            byte* dp = (byte*)this.destination.Pin();
            int count = this.Count;
            int size = sizeof(Rgba32);

            for (int i = 0; i < count; i++)
            {
                Vector4 v = Unsafe.Read<Vector4>(sp);
                Rgba32 c = default(Rgba32);
                c.PackFromVector4(v);
                Unsafe.Write(dp, c);

                sp++;
                dp += size;
            }
        }

        [Benchmark]
        public void PackUsingReferences()
        {
            ref Vector4 sp = ref this.source.Array[0];
            ref Rgba32 dp = ref this.destination.Array[0];
            int count = this.Count;

            for (int i = 0; i < count; i++)
            {
                dp.PackFromVector4(sp);

                sp = Unsafe.Add(ref sp, 1);
                dp = Unsafe.Add(ref dp, 1);
            }
        }
    }
}