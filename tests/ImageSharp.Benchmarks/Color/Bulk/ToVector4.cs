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
            //512
            256
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

        //[Benchmark]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        //[Benchmark]
        public void OptimizedBulk()
        {
            PixelOperations<TPixel>.Instance.ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    [RyuJitX64Job]
    [DisassemblyDiagnoser(printAsm: true, printSource: true)]
    public class ToVector4_Rgba32 : ToVector4<Rgba32>
    {
        class Config : ManualConfig
        {
        }

        [Benchmark(Baseline = true)]
        public void FastScalarBulk()
        {
            ref Rgba32 sBase = ref this.source.GetSpan()[0];
            ref Vector4 dBase = ref this.destination.GetSpan()[0];

            for (int i = 0; i < this.Count; i++)
            {
                ref Rgba32 s = ref Unsafe.Add(ref sBase, i);
                ref Vector4 d = ref Unsafe.Add(ref dBase, i);
                d.X = s.R;
                d.Y = s.G;
                d.Z = s.B;
                d.W = s.A;
            }
        }

        [Benchmark]
        public void BulkConvertByteToNormalizedFloat()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.BulkConvertByteToNormalizedFloat(sBytes, dFloats);
        }

        [Benchmark]
        public void ExtendedIntrinsics_BulkConvertByteToNormalizedFloat()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.ExtendedIntrinsics.BulkConvertByteToNormalizedFloat(sBytes, dFloats);
        }

        //[Benchmark]
        public void Original()
        {
            ToVector4SimdAligned(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ToVector4SimdAligned(ReadOnlySpan<Rgba32> sourceColors, Span<Vector4> destVectors, int count)
        {
            if (!Vector.IsHardwareAccelerated)
            {
                throw new InvalidOperationException(
                    "Rgba32.PixelOperations.ToVector4SimdAligned() should not be called when Vector.IsHardwareAccelerated == false!");
            }

            DebugGuard.IsTrue(
                count % Vector<uint>.Count == 0,
                nameof(count),
                "Argument 'count' should divisible by Vector<uint>.Count!");

            var bVec = new Vector<float>(256.0f / 255.0f);
            var magicFloat = new Vector<float>(32768.0f);
            var magicInt = new Vector<uint>(1191182336); // reinterpreded value of 32768.0f
            var mask = new Vector<uint>(255);

            int unpackedRawCount = count * 4;

            ref uint sourceBase = ref Unsafe.As<Rgba32, uint>(ref MemoryMarshal.GetReference(sourceColors));
            ref UnpackedRGBA destBaseAsUnpacked = ref Unsafe.As<Vector4, UnpackedRGBA>(ref MemoryMarshal.GetReference(destVectors));
            ref Vector<uint> destBaseAsUInt = ref Unsafe.As<UnpackedRGBA, Vector<uint>>(ref destBaseAsUnpacked);
            ref Vector<float> destBaseAsFloat = ref Unsafe.As<UnpackedRGBA, Vector<float>>(ref destBaseAsUnpacked);

            for (int i = 0; i < count; i++)
            {
                uint sVal = Unsafe.Add(ref sourceBase, i);
                ref UnpackedRGBA dst = ref Unsafe.Add(ref destBaseAsUnpacked, i);

                // This call is the bottleneck now:
                dst.Load(sVal);
            }

            int numOfVectors = unpackedRawCount / Vector<uint>.Count;

            for (int i = 0; i < numOfVectors; i++)
            {
                Vector<uint> vi = Unsafe.Add(ref destBaseAsUInt, i);

                vi &= mask;
                vi |= magicInt;

                var vf = Vector.AsVectorSingle(vi);
                vf = (vf - magicFloat) * bVec;

                Unsafe.Add(ref destBaseAsFloat, i) = vf;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UnpackedRGBA
        {
            private uint r;

            private uint g;

            private uint b;

            private uint a;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Load(uint p)
            {
                this.r = p;
                this.g = p >> 8;
                this.b = p >> 16;
                this.a = p >> 24;
            }
        }
    }
}