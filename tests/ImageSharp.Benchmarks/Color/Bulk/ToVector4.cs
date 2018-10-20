// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System.Buffers;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    public abstract class ToVector4<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        protected IMemoryOwner<TPixel> source;

        protected IMemoryOwner<Vector4> destination;

        [Params(
            //64, 
            //256,
            //512,
            //1024,
            2048
            )]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.source = Configuration.Default.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.destination = Configuration.Default.MemoryAllocator.Allocate<Vector4>(this.Count);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.source.Dispose();
            this.destination.Dispose();
        }

        //[Benchmark]
        public void PerElement()
        {
            Span<TPixel> s = this.source.GetSpan();
            Span<Vector4> d = this.destination.GetSpan();

            for (int i = 0; i < this.Count; i++)
            {
                d[i] = s[i].ToVector4();
            }
        }

        [Benchmark]
        public void PixelOperations_Base()
        {
            new PixelOperations<TPixel>().ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [Benchmark]
        public void PixelOperations_Specialized()
        {
            PixelOperations<TPixel>.Instance.ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    [Config(typeof(Config.ShortClr))]
    public class ToVector4_Rgba32 : ToVector4<Rgba32>
    {
        [Benchmark]
        public void BasicBulk()
        {
            ref Rgba32 sBase = ref this.source.GetSpan()[0];
            ref Vector4 dBase = ref this.destination.GetSpan()[0];

            Vector4 scale = new Vector4(1f / 255f);

            Vector4 v = default;

            for (int i = 0; i < this.Count; i++)
            {
                ref Rgba32 s = ref Unsafe.Add(ref sBase, i);
                v.X = s.R;
                v.Y = s.G;
                v.Z = s.B;
                v.W = s.A;
                v *= scale;
                Unsafe.Add(ref dBase, i) = v;
            }
        }
        
        [Benchmark(Baseline = true)]
        public void BasicIntrinsics256_BulkConvertByteToNormalizedFloat()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.BasicIntrinsics256.BulkConvertByteToNormalizedFloat(sBytes, dFloats);
        }

        [Benchmark]
        public void ExtendedIntrinsics_BulkConvertByteToNormalizedFloat()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.ExtendedIntrinsics.BulkConvertByteToNormalizedFloat(sBytes, dFloats);
        }

        //[Benchmark]
        public void ExtendedIntrinsics_BulkConvertByteToNormalizedFloat_2Loops()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            int n = dFloats.Length / Vector<byte>.Count;

            ref Vector<byte> sourceBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference((ReadOnlySpan<byte>)sBytes));
            ref Vector<float> destBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(dFloats));
            ref Vector<uint> destBaseU = ref Unsafe.As<Vector<float>, Vector<uint>>(ref destBase);

            for (int i = 0; i < n; i++)
            {
                Vector<byte> b = Unsafe.Add(ref sourceBase, i);

                Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
                Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
                Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

                ref Vector<uint> d = ref Unsafe.Add(ref destBaseU, i * 4);
                d = w0;
                Unsafe.Add(ref d, 1) = w1;
                Unsafe.Add(ref d, 2) = w2;
                Unsafe.Add(ref d, 3) = w3;
            }

            n = dFloats.Length / Vector<float>.Count;
            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
            {
                ref Vector<float> dRef = ref Unsafe.Add(ref destBase, i);

                Vector<int> du = Vector.AsVectorInt32(dRef);
                Vector<float> v = Vector.ConvertToSingle(du);
                v *= scale;

                dRef = v;
            }
        }

        [Benchmark]
        public void ExtendedIntrinsics_BulkConvertByteToNormalizedFloat_ConvertInSameLoop()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            int n = dFloats.Length / Vector<byte>.Count;

            ref Vector<byte> sourceBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference((ReadOnlySpan<byte>)sBytes));
            ref Vector<float> destBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(dFloats));
            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
            {
                Vector<byte> b = Unsafe.Add(ref sourceBase, i);

                Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
                Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
                Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

                Vector<float> f0 = ConvertToNormalizedSingle(w0, scale);
                Vector<float> f1 = ConvertToNormalizedSingle(w1, scale);
                Vector<float> f2 = ConvertToNormalizedSingle(w2, scale);
                Vector<float> f3 = ConvertToNormalizedSingle(w3, scale);

                ref Vector<float> d = ref Unsafe.Add(ref destBase, i * 4);
                d = f0;
                Unsafe.Add(ref d, 1) = f1;
                Unsafe.Add(ref d, 2) = f2;
                Unsafe.Add(ref d, 3) = f3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<float> ConvertToNormalizedSingle(Vector<uint> u, Vector<float> scale)
        {
            Vector<int> vi = Vector.AsVectorInt32(u);
            Vector<float> v = Vector.ConvertToSingle(vi);
            v *= scale;
            return v;
        }
    }
}