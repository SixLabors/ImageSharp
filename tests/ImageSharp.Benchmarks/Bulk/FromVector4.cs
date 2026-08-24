// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Bulk;

public abstract class FromVector4<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    protected IMemoryOwner<Vector4> Source { get; set; }

    protected IMemoryOwner<TPixel> Destination { get; set; }

    protected Configuration Configuration => Configuration.Default;

    // [Params(64, 2048)]
    [Params(64, 256, 2048)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.Destination = this.Configuration.MemoryAllocator.Allocate<TPixel>(this.Count);
        this.Source = this.Configuration.MemoryAllocator.Allocate<Vector4>(this.Count);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.Destination.Dispose();
        this.Source.Dispose();
    }

    // [Benchmark]
    public void PerElement()
    {
        ref Vector4 s = ref MemoryMarshal.GetReference(this.Source.GetSpan());
        ref TPixel d = ref MemoryMarshal.GetReference(this.Destination.GetSpan());
        for (nuint i = 0; i < (uint)this.Count; i++)
        {
            Unsafe.Add(ref d, i) = TPixel.FromVector4(Unsafe.Add(ref s, i));
        }
    }

    [Benchmark(Baseline = true)]
    public void PixelOperations_Base()
        => new PixelOperations<TPixel>().FromVector4Destructive(this.Configuration, this.Source.GetSpan(), this.Destination.GetSpan());

    [Benchmark]
    public void PixelOperations_Specialized()
        => PixelOperations<TPixel>.Instance.FromVector4Destructive(this.Configuration, this.Source.GetSpan(), this.Destination.GetSpan());
}

[Config(typeof(Config.Short))]
public class FromVector4Rgba32 : FromVector4<Rgba32>
{
    /// <summary>
    /// Measures the raw SIMD kernel that converts normalized RGBA vector components to bytes.
    /// </summary>
    [Benchmark]
    public void UseHwIntrinsics()
    {
        Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.Source.GetSpan());
        Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.Destination.GetSpan());

        // Four components per pixel make every configured count divisible by the supported 128-, 256-, and 512-bit
        // byte-vector widths. Calling the block kernel directly therefore excludes PixelOperations contract handling
        // and the general conversion wrapper's remainder dispatch, establishing the lower bound for the SIMD conversion.
        SimdUtils.HwIntrinsics.NormalizedFloatToByteSaturate(sBytes, dFloats);
    }

    private static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => [0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0
    ];

    [Benchmark]
    public void UseAvx2_Grouped()
    {
        Span<float> src = MemoryMarshal.Cast<Vector4, float>(this.Source.GetSpan());
        Span<byte> dest = MemoryMarshal.Cast<Rgba32, byte>(this.Destination.GetSpan());

        nuint n = (uint)dest.Length / (uint)Vector<byte>.Count;

        ref Vector256<float> sourceBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(src));
        ref Vector256<byte> destBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(dest));

        ref byte maskBase = ref MemoryMarshal.GetReference(PermuteMaskDeinterleave8x32);
        Vector256<int> mask = Unsafe.As<byte, Vector256<int>>(ref maskBase);

        Vector256<float> maxBytes = Vector256.Create(255f);

        for (nuint i = 0; i < n; i++)
        {
            ref Vector256<float> s = ref Unsafe.Add(ref sourceBase, i * 4);

            Vector256<float> f0 = s;
            Vector256<float> f1 = Unsafe.Add(ref s, 1);
            Vector256<float> f2 = Unsafe.Add(ref s, 2);
            Vector256<float> f3 = Unsafe.Add(ref s, 3);

            f0 = Avx.Multiply(maxBytes, f0);
            f1 = Avx.Multiply(maxBytes, f1);
            f2 = Avx.Multiply(maxBytes, f2);
            f3 = Avx.Multiply(maxBytes, f3);

            Vector256<int> w0 = Avx.ConvertToVector256Int32(f0);
            Vector256<int> w1 = Avx.ConvertToVector256Int32(f1);
            Vector256<int> w2 = Avx.ConvertToVector256Int32(f2);
            Vector256<int> w3 = Avx.ConvertToVector256Int32(f3);

            Vector256<short> u0 = Avx2.PackSignedSaturate(w0, w1);
            Vector256<short> u1 = Avx2.PackSignedSaturate(w2, w3);
            Vector256<byte> b = Avx2.PackUnsignedSaturate(u0, u1);
            b = Avx2.PermuteVar8x32(b.AsInt32(), mask).AsByte();

            Unsafe.Add(ref destBase, i) = b;
        }
    }

    /*
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET 8.0.28, X64 RyuJIT x86-64-v4

    | Method                      | Count | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated |
    |---------------------------- |------ |------------:|-----------:|----------:|------:|--------:|----------:|
    | PixelOperations_Base        | 64    |    60.19 ns |  32.376 ns |  1.775 ns |  1.00 |    0.04 |         - |
    | PixelOperations_Specialized | 64    |    22.94 ns |  66.082 ns |  3.622 ns |  0.38 |    0.05 |         - |
    | UseHwIntrinsics             | 64    |    10.64 ns |   9.535 ns |  0.523 ns |  0.18 |    0.01 |         - |
    | UseAvx2_Grouped             | 64    |    11.69 ns |   0.997 ns |  0.055 ns |  0.19 |    0.01 |         - |
    | PixelOperations_Base        | 256   |   232.65 ns |  37.948 ns |  2.080 ns |  1.00 |    0.01 |         - |
    | PixelOperations_Specialized | 256   |    33.75 ns |   3.906 ns |  0.214 ns |  0.15 |    0.00 |         - |
    | UseHwIntrinsics             | 256   |    21.91 ns |   2.732 ns |  0.150 ns |  0.09 |    0.00 |         - |
    | UseAvx2_Grouped             | 256   |    24.84 ns |   5.333 ns |  0.292 ns |  0.11 |    0.00 |         - |
    | PixelOperations_Base        | 2048  | 1,500.98 ns | 951.510 ns | 52.155 ns |  1.00 |    0.04 |         - |
    | PixelOperations_Specialized | 2048  |   156.21 ns |  65.074 ns |  3.567 ns |  0.10 |    0.00 |         - |
    | UseHwIntrinsics             | 2048  |   144.38 ns |  40.947 ns |  2.244 ns |  0.10 |    0.00 |         - |
    | UseAvx2_Grouped             | 2048  |   189.80 ns |  54.098 ns |  2.965 ns |  0.13 |    0.00 |         - |
    */
}

/// <summary>
/// Measures bulk conversion from <see cref="Vector4"/> values to premultiplied <see cref="Bgra32P"/> pixels.
/// </summary>
[Config(typeof(Config.Analysis))]
public class FromVector4Bgra32P : FromVector4<Bgra32P>
{
    /*
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET 8.0.28, X64 RyuJIT x86-64-v4

    | Method                      | Count | Mean        | Error    | StdDev    | Ratio | Code Size | Allocated |
    |---------------------------- |------ |------------:|---------:|----------:|------:|----------:|----------:|
    | PixelOperations_Base        | 64    |    53.35 ns | 0.208 ns |  0.311 ns |  1.00 |   1,152 B |         - |
    | PixelOperations_Specialized | 64    |    46.61 ns | 0.195 ns |  0.280 ns |  0.87 |   2,975 B |         - |
    | PixelOperations_Base        | 256   |   212.11 ns | 0.719 ns |  1.076 ns |  1.00 |   1,152 B |         - |
    | PixelOperations_Specialized | 256   |    77.12 ns | 0.824 ns |  1.233 ns |  0.36 |   2,992 B |         - |
    | PixelOperations_Base        | 2048  | 1,435.87 ns | 9.402 ns | 13.781 ns |  1.00 |   1,152 B |         - |
    | PixelOperations_Specialized | 2048  |   250.89 ns | 9.354 ns | 13.416 ns |  0.17 |   2,992 B |         - |
    */
}
