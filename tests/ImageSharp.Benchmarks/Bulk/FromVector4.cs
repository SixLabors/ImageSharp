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

[Config(typeof(Config.Short))]
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

public class FromVector4Rgba32 : FromVector4<Rgba32>
{
    [Benchmark]
    public void UseHwIntrinsics()
    {
        Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.Source.GetSpan());
        Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.Destination.GetSpan());

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
    BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3085/23H2/2023Update/SunValley3)
    11th Gen Intel Core i7-11370H 3.30GHz, 1 CPU, 8 logical and 4 physical cores
    .NET SDK 8.0.200-preview.23624.5
      [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
      Job-YJYLLR : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

    Runtime=.NET 8.0  Arguments=/p:DebugType=portable  IterationCount=3
    LaunchCount=1  WarmupCount=3

    | Method                      | Count | Mean        | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
    |---------------------------- |------ |------------:|-------------:|-----------:|------:|--------:|----------:|------------:|
    | PixelOperations_Base        | 64    |   114.80 ns |    16.459 ns |   0.902 ns |  1.00 |    0.00 |         - |          NA |
    | PixelOperations_Specialized | 64    |    28.91 ns |    80.482 ns |   4.411 ns |  0.25 |    0.04 |         - |          NA |
    | FallbackIntrinsics128       | 64    |   133.60 ns |    23.750 ns |   1.302 ns |  1.16 |    0.02 |         - |          NA |
    | ExtendedIntrinsic           | 64    |    40.11 ns |    10.183 ns |   0.558 ns |  0.35 |    0.01 |         - |          NA |
    | UseHwIntrinsics             | 64    |    14.71 ns |     4.860 ns |   0.266 ns |  0.13 |    0.00 |         - |          NA |
    | UseAvx2_Grouped             | 64    |    20.23 ns |    11.619 ns |   0.637 ns |  0.18 |    0.00 |         - |          NA |
    |                             |       |             |              |            |       |         |           |             |
    | PixelOperations_Base        | 256   |   387.94 ns |    31.591 ns |   1.732 ns |  1.00 |    0.00 |         - |          NA |
    | PixelOperations_Specialized | 256   |    50.93 ns |    22.388 ns |   1.227 ns |  0.13 |    0.00 |         - |          NA |
    | FallbackIntrinsics128       | 256   |   509.72 ns |   249.926 ns |  13.699 ns |  1.31 |    0.04 |         - |          NA |
    | ExtendedIntrinsic           | 256   |   140.32 ns |     9.353 ns |   0.513 ns |  0.36 |    0.00 |         - |          NA |
    | UseHwIntrinsics             | 256   |    41.99 ns |    16.000 ns |   0.877 ns |  0.11 |    0.00 |         - |          NA |
    | UseAvx2_Grouped             | 256   |    63.81 ns |     2.360 ns |   0.129 ns |  0.16 |    0.00 |         - |          NA |
    |                             |       |             |              |            |       |         |           |             |
    | PixelOperations_Base        | 2048  | 2,979.49 ns | 2,023.706 ns | 110.926 ns |  1.00 |    0.00 |         - |          NA |
    | PixelOperations_Specialized | 2048  |   326.19 ns |    19.077 ns |   1.046 ns |  0.11 |    0.00 |         - |          NA |
    | FallbackIntrinsics128       | 2048  | 3,885.95 ns |   411.078 ns |  22.533 ns |  1.31 |    0.05 |         - |          NA |
    | ExtendedIntrinsic           | 2048  | 1,078.58 ns |   136.960 ns |   7.507 ns |  0.36 |    0.01 |         - |          NA |
    | UseHwIntrinsics             | 2048  |   312.07 ns |    68.662 ns |   3.764 ns |  0.10 |    0.00 |         - |          NA |
    | UseAvx2_Grouped             | 2048  |   451.83 ns |    41.742 ns |   2.288 ns |  0.15 |    0.01 |         - |          NA |
    */
}
