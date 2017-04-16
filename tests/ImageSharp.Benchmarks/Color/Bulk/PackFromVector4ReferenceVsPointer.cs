namespace ImageSharp.Benchmarks
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;

    using ImageSharp;

    /// <summary>
    /// Compares two implementation candidates for general BulkPixelOperations.ToVector4():
    /// - One iterating with pointers
    /// - One iterating with ref locals
    /// </summary>
    public unsafe class PackFromVector4ReferenceVsPointer
    {
        private Buffer<ImageSharp.Color32> destination;

        private Buffer<Vector4> source;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.destination = new Buffer<ImageSharp.Color32>(this.Count);
            this.source = new Buffer<Vector4>(this.Count * 4);
            this.source.Pin();
            this.destination.Pin();
        }

        [Cleanup]
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
            int size = sizeof(ImageSharp.Color32);

            for (int i = 0; i < count; i++)
            {
                Vector4 v = Unsafe.Read<Vector4>(sp);
                ImageSharp.Color32 c = default(ImageSharp.Color32);
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
            ref ImageSharp.Color32 dp = ref this.destination.Array[0];
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