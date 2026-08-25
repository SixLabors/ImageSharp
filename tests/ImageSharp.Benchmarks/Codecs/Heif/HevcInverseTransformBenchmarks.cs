// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures complete coded-frame traversal through HEVC inverse-transform reconstruction.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class HevcInverseTransformBenchmarks
{
    /// <summary>
    /// The coded frame width, which is an exact multiple of the maximum transform-block side.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The coded frame height including the final padded coding-tree row for a 1080-line presentation.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The base-two logarithm of the benchmark transform-block side.
    /// </summary>
    private const int BlockLog2 = 5;

    /// <summary>
    /// The transform-block side in samples.
    /// </summary>
    private const int BlockSize = 1 << BlockLog2;

    /// <summary>
    /// The deterministic dequantized coefficients reused by each benchmark block.
    /// </summary>
    private readonly int[] coefficients = new int[BlockSize * BlockSize];

    /// <summary>
    /// The frame-wide predicted and reconstructed samples.
    /// </summary>
    private readonly ushort[] destination = new ushort[Width * Height];

    /// <summary>
    /// The maximum-block inverse-transform scratch reused throughout each coded frame.
    /// </summary>
    private readonly int[] scratch = new int[HevcInverseTransformer.GetScratchLength(BlockLog2, BlockLog2)];

    /// <summary>
    /// Populates a dense, deterministic twelve-bit transform workload outside the measured frame traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < this.coefficients.Length; i++)
        {
            this.coefficients[i] = (((i * 37) + 11) % 127) - 63;
        }

        this.destination.AsSpan().Fill(2048);
    }

    /// <summary>
    /// Measures factorized inverse DCT, both transpositions, and saturated prediction addition for a coded frame.
    /// </summary>
    /// <returns>The final reconstructed sample, keeping the frame output observable.</returns>
    [Benchmark]
    public ushort TransformFrame()
    {
        for (int y = 0; y < Height; y += BlockSize)
        {
            for (int x = 0; x < Width; x += BlockSize)
            {
                HevcInverseTransformer.TransformAdd(
                    this.coefficients,
                    this.destination.AsSpan((y * Width) + x),
                    Width,
                    BlockLog2,
                    BlockLog2,
                    12,
                    18,
                    false,
                    this.scratch);
            }
        }

        return this.destination[^1];
    }
}
