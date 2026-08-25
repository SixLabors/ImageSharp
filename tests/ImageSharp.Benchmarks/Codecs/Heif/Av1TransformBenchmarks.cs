// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Compares scalar, SIMD, and runtime-dispatched AV1 forward and inverse transform blocks.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1TransformBenchmarks
{
    private readonly short[] spatial = new short[32 * 32];
    private readonly int[] coefficients = new int[32 * 32];
    private readonly byte[] prediction = new byte[32 * 32];
    private readonly byte[] reconstruction = new byte[32 * 32];
    private readonly int[] workspace = new int[Av1TransformWorkspace.MaximumLength];

    /// <summary>
    /// Initializes deterministic residual, coefficient, and prediction buffers outside the measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int index = 0; index < this.spatial.Length; index++)
        {
            this.spatial[index] = (short)(((index * 73) % 1023) - 511);
            this.coefficients[index] = ((index * 37) % 129) - 64;
            this.prediction[index] = (byte)(64 + ((index * 29) % 128));
        }
    }

    /// <summary>
    /// Measures the scalar eight-by-eight forward DCT traversal.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Forward8x8")]
    public int Forward8x8Scalar()
    {
        Av1Transform2dFlipConfiguration config = CreateForwardConfiguration(Av1TransformSize.Size8x8, 8);
        Av1ForwardTransformer.Transform2dScalar<Av1Dct8Forward1dOperator, Av1Dct8Forward1dOperator>(
            this.spatial, this.coefficients, 8, ref config, this.workspace);

        return this.coefficients[63];
    }

    /// <summary>
    /// Measures the Vector128 eight-by-eight forward DCT traversal.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward8x8")]
    public int Forward8x8Vector128()
    {
        Av1Transform2dFlipConfiguration config = CreateForwardConfiguration(Av1TransformSize.Size8x8, 8);
        Av1ForwardTransformer.Transform2dVector128<Av1Dct8Forward1dOperator, Av1Dct8Forward1dOperator>(
            this.spatial, this.coefficients, 8, ref config, this.workspace);

        return this.coefficients[63];
    }

    /// <summary>
    /// Measures the Vector256 eight-by-eight forward DCT traversal.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward8x8")]
    public int Forward8x8Vector256()
    {
        Av1Transform2dFlipConfiguration config = CreateForwardConfiguration(Av1TransformSize.Size8x8, 8);
        Av1ForwardTransformer.Transform2dVector256<Av1Dct8Forward1dOperator, Av1Dct8Forward1dOperator>(
            this.spatial, this.coefficients, 8, ref config, this.workspace);

        return this.coefficients[63];
    }

    /// <summary>
    /// Measures runtime dispatch of an eight-by-eight forward DCT block.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward8x8")]
    public int Forward8x8Dispatch()
    {
        Av1ForwardTransformer.Transform2d(
            this.spatial, this.coefficients, 8, Av1TransformType.DctDct, Av1TransformSize.Size8x8, 8, this.workspace);

        return this.coefficients[63];
    }

    /// <summary>
    /// Measures the scalar thirty-two-by-thirty-two forward DCT traversal.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Forward32x32")]
    public int Forward32x32Scalar()
    {
        Av1Transform2dFlipConfiguration config = CreateForwardConfiguration(Av1TransformSize.Size32x32, 10);
        Av1ForwardTransformer.Transform2dScalar<Av1Dct32Forward1dOperator, Av1Dct32Forward1dOperator>(
            this.spatial, this.coefficients, 32, ref config, this.workspace);

        return this.coefficients[^1];
    }

    /// <summary>
    /// Measures the Vector128 thirty-two-by-thirty-two forward DCT traversal.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward32x32")]
    public int Forward32x32Vector128()
    {
        Av1Transform2dFlipConfiguration config = CreateForwardConfiguration(Av1TransformSize.Size32x32, 10);
        Av1ForwardTransformer.Transform2dVector128<Av1Dct32Forward1dOperator, Av1Dct32Forward1dOperator>(
            this.spatial, this.coefficients, 32, ref config, this.workspace);

        return this.coefficients[^1];
    }

    /// <summary>
    /// Measures the Vector256 thirty-two-by-thirty-two forward DCT traversal.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward32x32")]
    public int Forward32x32Vector256()
    {
        Av1Transform2dFlipConfiguration config = CreateForwardConfiguration(Av1TransformSize.Size32x32, 10);
        Av1ForwardTransformer.Transform2dVector256<Av1Dct32Forward1dOperator, Av1Dct32Forward1dOperator>(
            this.spatial, this.coefficients, 32, ref config, this.workspace);

        return this.coefficients[^1];
    }

    /// <summary>
    /// Measures runtime dispatch of a thirty-two-by-thirty-two forward DCT block.
    /// </summary>
    /// <returns>The last coefficient written by the transform.</returns>
    [Benchmark]
    [BenchmarkCategory("Forward32x32")]
    public int Forward32x32Dispatch()
    {
        Av1ForwardTransformer.Transform2d(
            this.spatial, this.coefficients, 32, Av1TransformType.DctDct, Av1TransformSize.Size32x32, 10, this.workspace);

        return this.coefficients[^1];
    }

    /// <summary>
    /// Measures the scalar eight-by-eight inverse DCT and byte reconstruction traversal.
    /// </summary>
    /// <returns>The last reconstructed sample.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Inverse8x8")]
    public byte Inverse8x8Scalar()
    {
        Av1Transform2dFlipConfiguration config = CreateInverseConfiguration(Av1TransformSize.Size8x8, 8);
        Av1Inverse2dTransformer.Transform2dScalar<byte, Av1ByteInverseTransformOutputOperator, Av1Dct8Inverse1dOperator, Av1Dct8Inverse1dOperator>(
            this.coefficients, this.prediction, 8, this.reconstruction, 8, ref config, this.workspace, 8);

        return this.reconstruction[63];
    }

    /// <summary>
    /// Measures the Vector128 eight-by-eight inverse DCT and byte reconstruction traversal.
    /// </summary>
    /// <returns>The last reconstructed sample.</returns>
    [Benchmark]
    [BenchmarkCategory("Inverse8x8")]
    public byte Inverse8x8Vector128()
    {
        Av1Transform2dFlipConfiguration config = CreateInverseConfiguration(Av1TransformSize.Size8x8, 8);
        Av1Inverse2dTransformer.Transform2dVector128<byte, Av1ByteInverseTransformOutputOperator, Av1Dct8Inverse1dOperator, Av1Dct8Inverse1dOperator>(
            this.coefficients, this.prediction, 8, this.reconstruction, 8, ref config, this.workspace, 8);

        return this.reconstruction[63];
    }

    /// <summary>
    /// Measures the Vector256 eight-by-eight inverse DCT and byte reconstruction traversal.
    /// </summary>
    /// <returns>The last reconstructed sample.</returns>
    [Benchmark]
    [BenchmarkCategory("Inverse8x8")]
    public byte Inverse8x8Vector256()
    {
        Av1Transform2dFlipConfiguration config = CreateInverseConfiguration(Av1TransformSize.Size8x8, 8);
        Av1Inverse2dTransformer.Transform2dVector256<byte, Av1ByteInverseTransformOutputOperator, Av1Dct8Inverse1dOperator, Av1Dct8Inverse1dOperator>(
            this.coefficients, this.prediction, 8, this.reconstruction, 8, ref config, this.workspace, 8);

        return this.reconstruction[63];
    }

    /// <summary>
    /// Measures runtime dispatch of an eight-by-eight inverse DCT and byte reconstruction block.
    /// </summary>
    /// <returns>The last reconstructed sample.</returns>
    [Benchmark]
    [BenchmarkCategory("Inverse8x8")]
    public byte Inverse8x8Dispatch()
    {
        Av1InverseTransformer.Reconstruct8Bit(
            this.coefficients,
            this.prediction,
            8,
            this.reconstruction,
            8,
            Av1TransformSize.Size8x8,
            Av1TransformType.DctDct,
            0,
            64,
            false,
            this.workspace);

        return this.reconstruction[63];
    }

    /// <summary>
    /// Measures the scalar thirty-two-by-thirty-two inverse DCT and byte reconstruction traversal.
    /// </summary>
    /// <returns>The last reconstructed sample.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Inverse32x32")]
    public byte Inverse32x32Scalar()
    {
        Av1Transform2dFlipConfiguration config = CreateInverseConfiguration(Av1TransformSize.Size32x32, 8);
        Av1Inverse2dTransformer.Transform2dScalar<byte, Av1ByteInverseTransformOutputOperator, Av1Dct32Inverse1dOperator, Av1Dct32Inverse1dOperator>(
            this.coefficients, this.prediction, 32, this.reconstruction, 32, ref config, this.workspace, 8);

        return this.reconstruction[^1];
    }

    /// <summary>
    /// Measures the Vector128 thirty-two-by-thirty-two inverse DCT and byte reconstruction traversal.
    /// </summary>
    /// <returns>The last reconstructed sample.</returns>
    [Benchmark]
    [BenchmarkCategory("Inverse32x32")]
    public byte Inverse32x32Vector128()
    {
        Av1Transform2dFlipConfiguration config = CreateInverseConfiguration(Av1TransformSize.Size32x32, 8);
        Av1Inverse2dTransformer.Transform2dVector128<byte, Av1ByteInverseTransformOutputOperator, Av1Dct32Inverse1dOperator, Av1Dct32Inverse1dOperator>(
            this.coefficients, this.prediction, 32, this.reconstruction, 32, ref config, this.workspace, 8);

        return this.reconstruction[^1];
    }

    /// <summary>
    /// Measures the Vector256 thirty-two-by-thirty-two inverse DCT and byte reconstruction traversal.
    /// </summary>
    /// <returns>The last reconstructed sample.</returns>
    [Benchmark]
    [BenchmarkCategory("Inverse32x32")]
    public byte Inverse32x32Vector256()
    {
        Av1Transform2dFlipConfiguration config = CreateInverseConfiguration(Av1TransformSize.Size32x32, 8);
        Av1Inverse2dTransformer.Transform2dVector256<byte, Av1ByteInverseTransformOutputOperator, Av1Dct32Inverse1dOperator, Av1Dct32Inverse1dOperator>(
            this.coefficients, this.prediction, 32, this.reconstruction, 32, ref config, this.workspace, 8);

        return this.reconstruction[^1];
    }

    /// <summary>
    /// Measures runtime dispatch of a thirty-two-by-thirty-two inverse DCT and byte reconstruction block.
    /// </summary>
    /// <returns>The last reconstructed sample.</returns>
    [Benchmark]
    [BenchmarkCategory("Inverse32x32")]
    public byte Inverse32x32Dispatch()
    {
        Av1InverseTransformer.Reconstruct8Bit(
            this.coefficients,
            this.prediction,
            32,
            this.reconstruction,
            32,
            Av1TransformSize.Size32x32,
            Av1TransformType.DctDct,
            0,
            1024,
            false,
            this.workspace);

        return this.reconstruction[^1];
    }

    /// <summary>
    /// Creates a forward DCT configuration with the normative stage ranges for one coded bit depth.
    /// </summary>
    /// <param name="transformSize">The dimensions of the transform block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The initialized transform configuration.</returns>
    private static Av1Transform2dFlipConfiguration CreateForwardConfiguration(Av1TransformSize transformSize, int bitDepth)
        => Av1Transform2dFlipConfiguration.CreateForward(Av1TransformType.DctDct, transformSize, bitDepth);

    /// <summary>
    /// Creates an inverse DCT configuration with the normative stage ranges for one coded bit depth.
    /// </summary>
    /// <param name="transformSize">The dimensions of the transform block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The initialized transform configuration.</returns>
    private static Av1Transform2dFlipConfiguration CreateInverseConfiguration(Av1TransformSize transformSize, int bitDepth)
        => Av1Transform2dFlipConfiguration.CreateInverse(Av1TransformType.DctDct, transformSize, bitDepth);
}
