// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

public class PixelConversion_ConvertFromVector4
{
    private struct ConversionRunner<T>
        where T : struct, ITestPixel<T>
    {
        private T[] dest;

        private Vector4[] source;

        public ConversionRunner(int count)
        {
            this.dest = new T[count];
            this.source = new Vector4[count];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RunByRefConversion()
        {
            int count = this.dest.Length;

            ref T destBaseRef = ref this.dest[0];
            ref Vector4 sourceBaseRef = ref this.source[0];

            for (nuint i = 0; i < (uint)count; i++)
            {
                Unsafe.Add(ref destBaseRef, i).FromVector4(ref Unsafe.Add(ref sourceBaseRef, i));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RunByValConversion()
        {
            int count = this.dest.Length;

            ref T destBaseRef = ref this.dest[0];
            ref Vector4 sourceBaseRef = ref this.source[0];

            for (nuint i = 0; i < (uint)count; i++)
            {
                Unsafe.Add(ref destBaseRef, i).FromVector4(Unsafe.Add(ref sourceBaseRef, i));
            }
        }
    }

    private ConversionRunner<TestArgb> nonVectorRunner;

    private ConversionRunner<TestRgbaVector> vectorRunner;

    [Params(32)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.nonVectorRunner = new ConversionRunner<TestArgb>(this.Count);
        this.vectorRunner = new ConversionRunner<TestRgbaVector>(this.Count);
    }

    [Benchmark(Baseline = true)]
    public void VectorByRef()
    {
        this.vectorRunner.RunByRefConversion();
    }

    [Benchmark]
    public void VectorByVal()
    {
        this.vectorRunner.RunByValConversion();
    }

    [Benchmark]
    public void NonVectorByRef()
    {
        this.nonVectorRunner.RunByRefConversion();
    }

    [Benchmark]
    public void NonVectorByVal()
    {
        this.nonVectorRunner.RunByValConversion();
    }
}

/*
 * Results:
 *          Method | Count |       Mean |    StdDev | Scaled | Scaled-StdDev |
 * --------------- |------ |----------- |---------- |------- |-------------- |
 *     VectorByRef |    32 | 23.6678 ns | 0.1141 ns |   1.00 |          0.00 |
 *     VectorByVal |    32 | 24.5347 ns | 0.0771 ns |   1.04 |          0.01 |
 *  NonVectorByRef |    32 | 59.0187 ns | 0.2114 ns |   2.49 |          0.01 |
 *  NonVectorByVal |    32 | 58.7529 ns | 0.2545 ns |   2.48 |          0.02 |
 *
 *  !!! Conclusion !!!
 *  We do not need by-ref version of ConvertFromVector4() stuff
 */
