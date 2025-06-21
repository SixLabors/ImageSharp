// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

public abstract class PixelConversion_ConvertFromRgba32
{
    internal readonly struct ConversionRunner<T>
        where T : struct, ITestPixel<T>
    {
        public readonly T[] Destination;

        public readonly Rgba32[] Source;

        public ConversionRunner(int count)
        {
            this.Destination = new T[count];
            this.Source = new Rgba32[count];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void RunByRefConversion()
        {
            int count = this.Destination.Length;

            ref T destBaseRef = ref this.Destination[0];
            ref Rgba32 sourceBaseRef = ref this.Source[0];

            for (nuint i = 0; i < (uint)count; i++)
            {
                Unsafe.Add(ref destBaseRef, i).FromRgba32(ref Unsafe.Add(ref sourceBaseRef, i));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void RunByValConversion()
        {
            int count = this.Destination.Length;

            ref T destBaseRef = ref this.Destination[0];
            ref Rgba32 sourceBaseRef = ref this.Source[0];

            for (nuint i = 0; i < (uint)count; i++)
            {
                Unsafe.Add(ref destBaseRef, i).FromRgba32(Unsafe.Add(ref sourceBaseRef, i));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void RunStaticByValConversion()
        {
            int count = this.Destination.Length;

            ref T destBaseRef = ref this.Destination[0];
            ref Rgba32 sourceBaseRef = ref this.Source[0];

            for (nuint i = 0; i < (uint)count; i++)
            {
                Unsafe.Add(ref destBaseRef, i) = T.StaticFromRgba32(Unsafe.Add(ref sourceBaseRef, i));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void RunFromBytesConversion()
        {
            int count = this.Destination.Length;

            ref T destBaseRef = ref this.Destination[0];
            ref Rgba32 sourceBaseRef = ref this.Source[0];

            for (nuint i = 0; i < (uint)count; i++)
            {
                ref Rgba32 s = ref Unsafe.Add(ref sourceBaseRef, i);
                Unsafe.Add(ref destBaseRef, i).FromBytes(s.R, s.G, s.B, s.A);
            }
        }
    }

    internal ConversionRunner<TestRgba> CompatibleMemLayoutRunner;

    internal ConversionRunner<TestArgb> PermutedRunnerRgbaToArgb;

    internal ConversionRunner<TestRgbaVector> RunnerRgbaToRgbaVector;

    [Params(256, 2048)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.CompatibleMemLayoutRunner = new(this.Count);
        this.PermutedRunnerRgbaToArgb = new(this.Count);
        this.RunnerRgbaToRgbaVector = new(this.Count);
    }
}

public class PixelConversion_ConvertFromRgba32_Compatible : PixelConversion_ConvertFromRgba32
{
    [Benchmark(Baseline = true)]
    public void ByRef() => this.CompatibleMemLayoutRunner.RunByRefConversion();

    [Benchmark]
    public void ByVal() => this.CompatibleMemLayoutRunner.RunByValConversion();

    [Benchmark]
    public void StaticByVal() => this.CompatibleMemLayoutRunner.RunStaticByValConversion();

    [Benchmark]
    public void FromBytes() => this.CompatibleMemLayoutRunner.RunFromBytesConversion();

    [Benchmark]
    public void Inline()
    {
        ref Rgba32 sBase = ref this.CompatibleMemLayoutRunner.Source[0];
        ref Rgba32 dBase = ref Unsafe.As<TestRgba, Rgba32>(ref this.CompatibleMemLayoutRunner.Destination[0]);

        for (nuint i = 0; i < (uint)this.Count; i++)
        {
            Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, i);
        }
    }

    /*
    BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3007/23H2/2023Update/SunValley3)
    11th Gen Intel Core i7-11370H 3.30GHz, 1 CPU, 8 logical and 4 physical cores
    .NET SDK 8.0.200-preview.23624.5
      [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
      DefaultJob : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2


    | Method      | Count | Mean       | Error   | StdDev  | Ratio |
    |------------ |------ |-----------:|--------:|--------:|------:|
    | ByRef       | 256   |   103.4 ns | 0.52 ns | 0.46 ns |  1.00 |
    | ByVal       | 256   |   103.3 ns | 1.48 ns | 1.38 ns |  1.00 |
    | StaticByVal | 256   |   104.0 ns | 0.36 ns | 0.30 ns |  1.01 |
    | FromBytes   | 256   |   201.8 ns | 1.30 ns | 1.15 ns |  1.95 |
    | Inline      | 256   |   106.6 ns | 0.40 ns | 0.34 ns |  1.03 |
    |             |       |            |         |         |       |
    | ByRef       | 2048  |   771.5 ns | 3.68 ns | 3.27 ns |  1.00 |
    | ByVal       | 2048  |   769.7 ns | 3.39 ns | 2.83 ns |  1.00 |
    | StaticByVal | 2048  |   773.2 ns | 3.95 ns | 3.50 ns |  1.00 |
    | FromBytes   | 2048  | 1,555.3 ns | 9.24 ns | 8.19 ns |  2.02 |
    | Inline      | 2048  |   799.5 ns | 5.91 ns | 4.93 ns |  1.04 |
    */
}

public class PixelConversion_ConvertFromRgba32_Permuted_RgbaToArgb : PixelConversion_ConvertFromRgba32
{
    [Benchmark(Baseline = true)]
    public void ByRef() => this.PermutedRunnerRgbaToArgb.RunByRefConversion();

    [Benchmark]
    public void ByVal() => this.PermutedRunnerRgbaToArgb.RunByValConversion();

    [Benchmark]
    public void StaticByVal() => this.PermutedRunnerRgbaToArgb.RunStaticByValConversion();

    [Benchmark]
    public void FromBytes() => this.PermutedRunnerRgbaToArgb.RunFromBytesConversion();

    [Benchmark]
    public void InlineShuffle()
    {
        ref Rgba32 sBase = ref this.PermutedRunnerRgbaToArgb.Source[0];
        ref TestArgb dBase = ref this.PermutedRunnerRgbaToArgb.Destination[0];

        for (nuint i = 0; i < (uint)this.Count; i++)
        {
            Rgba32 s = Unsafe.Add(ref sBase, i);
            ref TestArgb d = ref Unsafe.Add(ref dBase, i);

            d.R = s.R;
            d.G = s.G;
            d.B = s.B;
            d.A = s.A;
        }
    }

    // Commenting this out because for some reason MSBuild is showing  error CS0029: Cannot implicitly convert type 'System.ReadOnlySpan<byte>' to 'System.Span<byte>'
    // when trying to build via BenchmarkDotnet. (╯‵□′)╯︵┻━┻
    // [Benchmark]
    // public void PixelConverter_Rgba32_ToArgb32()
    // {
    //    ReadOnlySpan<byte> source = MemoryMarshal.Cast<Rgba32, byte>(this.PermutedRunnerRgbaToArgb.Source);
    //    Span<byte> destination = MemoryMarshal.Cast<TestArgb, byte>(this.PermutedRunnerRgbaToArgb.Destination);
    //
    //    PixelConverter.FromRgba32.ToArgb32(source, destination);
    // }

    /*
    BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3007/23H2/2023Update/SunValley3)
    11th Gen Intel Core i7-11370H 3.30GHz, 1 CPU, 8 logical and 4 physical cores
    .NET SDK 8.0.200-preview.23624.5
      [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
      DefaultJob : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2


    | Method                         | Count | Mean        | Error     | StdDev   | Ratio | RatioSD |
    |------------------------------- |------ |------------:|----------:|---------:|------:|--------:|
    | ByRef                          | 256   |   203.48 ns |  3.318 ns | 3.104 ns |  1.00 |    0.00 |
    | ByVal                          | 256   |   201.46 ns |  2.242 ns | 1.872 ns |  0.99 |    0.02 |
    | StaticByVal                    | 256   |   201.45 ns |  0.791 ns | 0.701 ns |  0.99 |    0.02 |
    | FromBytes                      | 256   |   200.76 ns |  1.365 ns | 1.140 ns |  0.99 |    0.01 |
    | InlineShuffle                  | 256   |   221.65 ns |  2.104 ns | 1.968 ns |  1.09 |    0.02 |
    | PixelConverter_Rgba32_ToArgb32 | 256   |    26.23 ns |  0.277 ns | 0.231 ns |  0.13 |    0.00 |
    |                                |       |             |           |          |       |         |
    | ByRef                          | 2048  | 1,561.54 ns | 11.208 ns | 8.751 ns |  1.00 |    0.00 |
    | ByVal                          | 2048  | 1,554.26 ns |  9.607 ns | 8.517 ns |  1.00 |    0.01 |
    | StaticByVal                    | 2048  | 1,562.48 ns |  8.937 ns | 8.360 ns |  1.00 |    0.01 |
    | FromBytes                      | 2048  | 1,552.68 ns |  7.445 ns | 5.812 ns |  0.99 |    0.01 |
    | InlineShuffle                  | 2048  | 1,711.28 ns |  7.559 ns | 6.312 ns |  1.10 |    0.01 |
    | PixelConverter_Rgba32_ToArgb32 | 2048  |    94.43 ns |  0.363 ns | 0.322 ns |  0.06 |    0.00 |
    */
}

public class PixelConversion_ConvertFromRgba32_RgbaToRgbaVector : PixelConversion_ConvertFromRgba32
{
    [Benchmark(Baseline = true)]
    public void ByRef() => this.RunnerRgbaToRgbaVector.RunByRefConversion();

    [Benchmark]
    public void ByVal() => this.RunnerRgbaToRgbaVector.RunByValConversion();

    [Benchmark]
    public void StaticByVal() => this.RunnerRgbaToRgbaVector.RunStaticByValConversion();

    /*
    BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3007/23H2/2023Update/SunValley3)
    11th Gen Intel Core i7-11370H 3.30GHz, 1 CPU, 8 logical and 4 physical cores
    .NET SDK 8.0.200-preview.23624.5
      [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
      DefaultJob : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2


    | Method      | Count | Mean       | Error    | StdDev   | Ratio | RatioSD |
    |------------ |------ |-----------:|---------:|---------:|------:|--------:|
    | ByRef       | 256   |   448.5 ns |  4.86 ns |  4.06 ns |  1.00 |    0.00 |
    | ByVal       | 256   |   447.0 ns |  1.55 ns |  1.21 ns |  1.00 |    0.01 |
    | StaticByVal | 256   |   447.4 ns |  1.67 ns |  1.30 ns |  1.00 |    0.01 |
    |             |       |            |          |          |       |         |
    | ByRef       | 2048  | 3,577.7 ns | 53.80 ns | 47.69 ns |  1.00 |    0.00 |
    | ByVal       | 2048  | 3,590.5 ns | 43.59 ns | 36.40 ns |  1.00 |    0.02 |
    | StaticByVal | 2048  | 3,604.6 ns | 16.19 ns | 14.36 ns |  1.01 |    0.01 |
    */
}
