// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures complete coded-frame traversal through HEVC transform-skip and residual differential reconstruction.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class HevcResidualReconstructionBenchmarks
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
    /// The benchmark transform-block side in samples.
    /// </summary>
    private const int BlockSize = 32;

    /// <summary>
    /// The deterministic dequantized coefficients reused by each benchmark block.
    /// </summary>
    private readonly int[] coefficients = new int[BlockSize * BlockSize];

    /// <summary>
    /// The reusable packed residual block.
    /// </summary>
    private readonly int[] residual = new int[BlockSize * BlockSize];

    /// <summary>
    /// Populates a dense, deterministic twelve-bit transform-skip workload outside the measured frame traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < this.coefficients.Length; i++)
        {
            this.coefficients[i] = (((i * 104729) + 4099) & 8191) - 4096;
        }
    }

    /// <summary>
    /// Measures frame-wide transform-skip normalization.
    /// </summary>
    /// <returns>The final residual, keeping the block output observable.</returns>
    [Benchmark(Baseline = true)]
    public int TransformSkipFrame() => this.ReconstructFrame(HevcResidualDpcmMode.None);

    /// <summary>
    /// Measures frame-wide transform-skip normalization followed by horizontal residual differential reconstruction.
    /// </summary>
    /// <returns>The final residual, keeping the block output observable.</returns>
    [Benchmark]
    public int HorizontalResidualDpcmFrame() => this.ReconstructFrame(HevcResidualDpcmMode.Horizontal);

    /// <summary>
    /// Measures frame-wide transform-skip normalization followed by vertical residual differential reconstruction.
    /// </summary>
    /// <returns>The final residual, keeping the block output observable.</returns>
    [Benchmark]
    public int VerticalResidualDpcmFrame() => this.ReconstructFrame(HevcResidualDpcmMode.Vertical);

    /// <summary>
    /// Reconstructs every maximum-size transform block in the coded benchmark frame.
    /// </summary>
    /// <param name="mode">The residual differential mode applied after transform-skip normalization.</param>
    /// <returns>The final reconstructed residual.</returns>
    private int ReconstructFrame(HevcResidualDpcmMode mode)
    {
        for (int y = 0; y < Height; y += BlockSize)
        {
            for (int x = 0; x < Width; x += BlockSize)
            {
                HevcResidualReconstructor.ApplyTransformSkip(this.coefficients, this.residual, BlockSize, BlockSize, 12, 18, 5, true, false);
                HevcResidualReconstructor.ApplyResidualDpcm(this.residual, BlockSize, BlockSize, mode);
            }
        }

        return this.residual[^1];
    }
}
