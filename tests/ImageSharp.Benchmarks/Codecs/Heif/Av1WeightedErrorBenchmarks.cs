// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Temporary: compares weight layouts for the matrix weighted transform error (av1_block_error_qm()), all with the
/// native 32 x 32 to 64 multiply for the squares.
/// </summary>
[Config(typeof(Configuration))]
public class Av1WeightedErrorBenchmarks
{
    private const int Shift = 10;
    private const long Rounding = 1L << (Shift - 1);

    private int[] coefficients;
    private int[] dequantized;
    private int[] intWeights;
    private byte[] byteWeights;
    private byte[] paddedMatrix;
    private short[] indices;
    private byte[] matrix;
    private short[] scan;
    private byte[] productionWeights;
    private Av1TransformSize transformSize;

    [Params(8, 32)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.transformSize = this.Size == 8 ? Av1TransformSize.Size8x8 : Av1TransformSize.Size32x32;
        Av1TransformSize transformSize = this.Size == 8 ? Av1TransformSize.Size8x8 : Av1TransformSize.Size32x32;
        int count = this.Size * this.Size;
        Random random = new(11);
        this.coefficients = new int[count];
        this.dequantized = new int[count];
        for (int i = 0; i < count; i++)
        {
            int value = random.Next(-400, 401) >> random.Next(0, 4);
            this.coefficients[i] = value;
            this.dequantized[i] = value / 40 * 40;
        }

        this.matrix = Av1QuantizationMatrixLookup.GetQuantizationMatrix(5, Av1Plane.Y, transformSize).ToArray();
        this.scan = Av1ScanOrderConstants.GetScanOrder(transformSize, Av1TransformType.DctDct).Scan.ToArray();
        this.paddedMatrix = new byte[this.matrix.Length + 3];
        this.matrix.CopyTo(this.paddedMatrix, 0);
        this.indices = new short[count];
        this.intWeights = new int[count];
        this.byteWeights = new byte[count];
        int index = 0;
        for (int row = 0; row < this.Size; row++)
        {
            for (int column = 0; column < this.Size; column++, index++)
            {
                short position = this.scan[(column * this.Size) + row];
                this.indices[index] = position;
                this.intWeights[index] = this.matrix[position];
                this.byteWeights[index] = this.matrix[position];
            }
        }

        this.productionWeights = Av1QuantizationMatrixLookup.GetDistortionWeights(5, Av1Plane.Y, transformSize).ToArray();
        long reference = this.LibaomScalar();
        Check(reference, this.ByteLayout(), nameof(this.ByteLayout));
        if (!this.productionWeights.AsSpan().SequenceEqual(this.byteWeights))
        {
            throw new InvalidOperationException("The static distortion weights differ from the scan order.");
        }
    }

    /// <summary>
    /// The reference loop: the weight of each coefficient is read through the scan at run time.
    /// </summary>
    [Benchmark(Baseline = true)]
    public long LibaomScalar()
    {
        int size = this.Size;
        long error = 0;
        long energy = 0;
        int index = 0;
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++, index++)
            {
                long weight = this.matrix[this.scan[(column * size) + row]];
                long weightedValue = this.coefficients[index] * weight;
                long weightedDifference = (this.coefficients[index] - (long)this.dequantized[index]) * weight;
                energy += ((weightedValue * weightedValue) + Rounding) >> Shift;
                error += ((weightedDifference * weightedDifference) + Rounding) >> Shift;
            }
        }

        return error + energy;
    }

    /// <summary>
    /// Weights stored as bytes in coefficient order, widened after the load.
    /// </summary>
    [Benchmark]
    public long ByteLayout()
    {
        ref int c = ref MemoryMarshal.GetArrayDataReference(this.coefficients);
        ref int d = ref MemoryMarshal.GetArrayDataReference(this.dequantized);
        ref byte w = ref MemoryMarshal.GetArrayDataReference(this.byteWeights);
        int length = this.coefficients.Length;
        int i = 0;
        Vector512<long> energies512 = Vector512<long>.Zero;
        Vector512<long> errors512 = Vector512<long>.Zero;
        Vector256<long> energies256 = Vector256<long>.Zero;
        Vector256<long> errors256 = Vector256<long>.Zero;
        if (Avx512F.IsSupported)
        {
            for (; i <= length - 16; i += 16)
            {
                Vector256<ushort> words = Vector256.WidenLower(Vector128.LoadUnsafe(ref w, (nuint)i).ToVector256Unsafe());
                Vector512<int> weights = Vector512.WidenLower(words.ToVector512Unsafe()).AsInt32();
                Accumulate(
                    Vector512.LoadUnsafe(ref c, (nuint)i),
                    Vector512.LoadUnsafe(ref d, (nuint)i),
                    weights,
                    ref energies512,
                    ref errors512);
            }
        }

        for (; i <= length - 8; i += 8)
        {
            Vector128<ushort> words = Vector128.WidenLower(Vector64.LoadUnsafe(ref Unsafe.Add(ref w, i)).ToVector128());
            Vector256<int> weights = Vector256.WidenLower(words.ToVector256Unsafe()).AsInt32();
            Accumulate(
                Vector256.LoadUnsafe(ref c, (nuint)i),
                Vector256.LoadUnsafe(ref d, (nuint)i),
                weights,
                ref energies256,
                ref errors256);
        }

        return Vector512.Sum(energies512 + errors512) + Vector256.Sum(energies256 + errors256);
    }

    /// <summary>
    /// The production kernel, which also applies the transform scale shift.
    /// </summary>
    [Benchmark]
    public long Production()
    {
        long error = Av1TransformBlockEncoder.GetWeightedTransformError(
            this.coefficients, this.dequantized, this.transformSize, Av1BitDepth.EightBit, this.productionWeights, out long energy);

        return error + energy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Accumulate(
        Vector512<int> values,
        Vector512<int> dequantized,
        Vector512<int> weights,
        ref Vector512<long> energies,
        ref Vector512<long> errors)
    {
        Vector512<long> rounding = Vector512.Create(Rounding);
        Vector512<int> weighted = values * weights;
        Vector512<int> weightedDifference = (values - dequantized) * weights;
        Vector512<int> weightedOdd = (weighted.AsInt64() >>> 32).AsInt32();
        Vector512<int> weightedDifferenceOdd = (weightedDifference.AsInt64() >>> 32).AsInt32();
        energies += ((Avx512F.Multiply(weighted, weighted) + rounding) >> Shift) +
            ((Avx512F.Multiply(weightedOdd, weightedOdd) + rounding) >> Shift);

        errors += ((Avx512F.Multiply(weightedDifference, weightedDifference) + rounding) >> Shift) +
            ((Avx512F.Multiply(weightedDifferenceOdd, weightedDifferenceOdd) + rounding) >> Shift);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Accumulate(
        Vector256<int> values,
        Vector256<int> dequantized,
        Vector256<int> weights,
        ref Vector256<long> energies,
        ref Vector256<long> errors)
    {
        Vector256<long> rounding = Vector256.Create(Rounding);
        Vector256<int> weighted = values * weights;
        Vector256<int> weightedDifference = (values - dequantized) * weights;
        Vector256<int> weightedOdd = (weighted.AsInt64() >>> 32).AsInt32();
        Vector256<int> weightedDifferenceOdd = (weightedDifference.AsInt64() >>> 32).AsInt32();
        energies += ((Avx2.Multiply(weighted, weighted) + rounding) >> Shift) +
            ((Avx2.Multiply(weightedOdd, weightedOdd) + rounding) >> Shift);

        errors += ((Avx2.Multiply(weightedDifference, weightedDifference) + rounding) >> Shift) +
            ((Avx2.Multiply(weightedDifferenceOdd, weightedDifferenceOdd) + rounding) >> Shift);
    }

    private static void Check(long expected, long actual, string name)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{name}: {actual} != {expected}");
        }
    }

    /// <summary>
    /// Measures the AVX-512 and AVX2 paths.
    /// </summary>
    public sealed class Configuration : ManualConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
        {
            this.AddJob(Job.ShortRun.WithId("Hardware").WithArguments([new MsBuildArgument("/p:RunAnalyzers=false")]));
            this.AddJob(
                Job.ShortRun
                    .WithId("Avx2")
                    .WithEnvironmentVariable("DOTNET_EnableAVX512", "0")
                    .WithArguments([new MsBuildArgument("/p:RunAnalyzers=false")]));
        }
    }
}
