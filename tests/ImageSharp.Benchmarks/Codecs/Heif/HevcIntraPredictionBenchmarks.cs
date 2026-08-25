// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures complete coded-frame traversal through representative HEVC intra-prediction modes.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class HevcIntraPredictionBenchmarks
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
    /// The base-two logarithm of the benchmark prediction-block side.
    /// </summary>
    private const int BlockLog2 = 5;

    /// <summary>
    /// The prediction-block side in samples.
    /// </summary>
    private const int BlockSize = 1 << BlockLog2;

    /// <summary>
    /// The prepared top reference shared by deterministic benchmark blocks.
    /// </summary>
    private readonly ushort[] top = new ushort[(BlockSize * 2) + 1];

    /// <summary>
    /// The prepared left reference shared by deterministic benchmark blocks.
    /// </summary>
    private readonly ushort[] left = new ushort[(BlockSize * 2) + 1];

    /// <summary>
    /// The frame-wide reconstructed prediction samples.
    /// </summary>
    private readonly ushort[] destination = new ushort[Width * Height];

    /// <summary>
    /// The maximum-block scratch reused throughout each coded frame.
    /// </summary>
    private readonly ushort[] scratch = new ushort[HevcIntraPredictor.GetScratchLength(BlockLog2)];

    /// <summary>
    /// Populates deterministic twelve-bit reference samples outside the measured frame traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.top[0] = this.left[0] = 1365;
        for (int i = 1; i < this.top.Length; i++)
        {
            this.top[i] = (ushort)((1365 + (37 * i)) & 4095);
            this.left[i] = (ushort)((1365 + (53 * i)) & 4095);
        }
    }

    /// <summary>
    /// Measures frame-wide planar prediction with 512-, 256-, and 128-bit row dispatch where available.
    /// </summary>
    /// <returns>The final reconstructed sample, keeping the frame output observable.</returns>
    [Benchmark(Baseline = true)]
    public ushort PredictPlanarFrame() => this.PredictFrame(0);

    /// <summary>
    /// Measures frame-wide fractional vertical prediction using contiguous SIMD interpolation.
    /// </summary>
    /// <returns>The final reconstructed sample, keeping the frame output observable.</returns>
    [Benchmark]
    public ushort PredictVerticalAngularFrame() => this.PredictFrame(30);

    /// <summary>
    /// Measures frame-wide horizontal prediction including the SIMD block transposition stage.
    /// </summary>
    /// <returns>The final reconstructed sample, keeping the frame output observable.</returns>
    [Benchmark]
    public ushort PredictHorizontalAngularFrame() => this.PredictFrame(2);

    /// <summary>
    /// Reconstructs every maximum-size prediction block in the coded benchmark frame.
    /// </summary>
    /// <param name="mode">The HEVC intra-prediction mode.</param>
    /// <returns>The final reconstructed sample.</returns>
    private ushort PredictFrame(int mode)
    {
        for (int y = 0; y < Height; y += BlockSize)
        {
            for (int x = 0; x < Width; x += BlockSize)
            {
                HevcIntraPredictor.Predict(
                    this.top,
                    this.left,
                    this.destination.AsSpan((y * Width) + x),
                    Width,
                    BlockLog2,
                    mode,
                    12,
                    true,
                    this.scratch);
            }
        }

        return this.destination[^1];
    }
}
