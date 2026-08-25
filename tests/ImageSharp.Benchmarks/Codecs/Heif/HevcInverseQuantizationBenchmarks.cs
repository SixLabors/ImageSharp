// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures complete coded-frame traversal through HEVC inverse quantization.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class HevcInverseQuantizationBenchmarks
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
    /// The deterministic quantized coefficients reused by each benchmark block.
    /// </summary>
    private readonly int[] quantized = new int[BlockSize * BlockSize];

    /// <summary>
    /// The reusable dequantized coefficient block.
    /// </summary>
    private readonly int[] destination = new int[BlockSize * BlockSize];

    /// <summary>
    /// The effective default scaling matrices.
    /// </summary>
    private readonly HevcScalingList scalingList = new();

    /// <summary>
    /// Populates a dense, deterministic twelve-bit transform workload outside the measured frame traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < this.quantized.Length; i++)
        {
            this.quantized[i] = (((i * 7919) + 257) & 131071) - 65536;
        }
    }

    /// <summary>
    /// Measures frame-wide inverse quantization using one uniform scale.
    /// </summary>
    /// <returns>The final coefficient, keeping the block output observable.</returns>
    [Benchmark(Baseline = true)]
    public int DequantizeFlatFrame() => this.DequantizeFrame(false);

    /// <summary>
    /// Measures frame-wide inverse quantization using an expanded thirty-two-by-thirty-two scaling matrix.
    /// </summary>
    /// <returns>The final coefficient, keeping the block output observable.</returns>
    [Benchmark]
    public int DequantizeScalingListFrame() => this.DequantizeFrame(true);

    /// <summary>
    /// Dequantizes every maximum-size transform block in the coded benchmark frame.
    /// </summary>
    /// <param name="scalingListEnabled">Whether the default intra-luma scaling matrix applies.</param>
    /// <returns>The final dequantized coefficient.</returns>
    private int DequantizeFrame(bool scalingListEnabled)
    {
        for (int y = 0; y < Height; y += BlockSize)
        {
            for (int x = 0; x < Width; x += BlockSize)
            {
                HevcInverseQuantizer.Dequantize(
                    this.quantized,
                    this.destination,
                    BlockLog2,
                    12,
                    18,
                    75,
                    scalingListEnabled,
                    this.scalingList,
                    HevcPlane.Y,
                    true,
                    false,
                    true);
            }
        }

        return this.destination[^1];
    }
}
