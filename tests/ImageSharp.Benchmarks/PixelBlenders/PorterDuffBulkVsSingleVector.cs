// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.Benchmarks.PixelBlenders;

public class PorterDuffBulkVsSingleVector
{
    private Vector4[] backdrop;
    private Vector4[] source;

    [GlobalSetup]
    public void Setup()
    {
        this.backdrop = new Vector4[8 * 20];
        this.source = new Vector4[8 * 20];

        FillRandom(this.backdrop);
        FillRandom(this.source);
    }

    private static void FillRandom(Vector4[] arr)
    {
        Random rng = new();
        for (int i = 0; i < arr.Length; i++)
        {
            arr[i].X = rng.NextSingle();
            arr[i].Y = rng.NextSingle();
            arr[i].Z = rng.NextSingle();
            arr[i].W = rng.NextSingle();
        }
    }

    [Benchmark(Description = "Scalar", Baseline = true)]
    public Vector4 OverlayValueFunction_Scalar()
    {
        Vector4 result = default;
        for (int i = 0; i < this.backdrop.Length; i++)
        {
            result = PorterDuffFunctions.NormalSrcOver(this.backdrop[i], this.source[i], .5F);
        }

        return result;
    }

    [Benchmark(Description = "Avx")]
    public Vector256<float> OverlayValueFunction_Avx()
    {
        ref Vector256<float> backdrop = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference<Vector4>(this.backdrop));
        ref Vector256<float> source = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference<Vector4>(this.source));

        Vector256<float> result = default;
        Vector256<float> opacity = Vector256.Create(.5F);
        int count = this.backdrop.Length / 2;
        for (int i = 0; i < count; i++)
        {
            result = PorterDuffFunctions.NormalSrcOver(Unsafe.Add(ref backdrop, i), Unsafe.Add(ref source, i), opacity);
        }

        return result;
    }
}
