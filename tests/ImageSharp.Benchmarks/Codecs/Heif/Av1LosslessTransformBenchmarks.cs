// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures frame-wide AV1 lossless inverse-transform reconstruction.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1LosslessTransformBenchmarks
{
    /// <summary>
    /// The coded frame width in samples.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The coded frame height including the final padded coding-tree row for a 1080-line presentation.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The lossless AV1 transform side in samples.
    /// </summary>
    private const int TransformSize = 4;

    /// <summary>
    /// The dense dequantized coefficient block used by every transform.
    /// </summary>
    private readonly int[] coefficients =
    [
        320, -192, 64, -448,
        128, 256, -320, 96,
        -224, 160, 384, -128,
        448, -64, -256, 192
    ];

    /// <summary>
    /// The caller-owned transform workspace reused across the frame.
    /// </summary>
    private readonly int[] workspace = new int[Av1TransformWorkspace.MaximumLength];

    /// <summary>
    /// The frame-wide eight-bit reconstruction surface.
    /// </summary>
    private readonly byte[] destination8 = new byte[Width * Height];

    /// <summary>
    /// The frame-wide twelve-bit reconstruction surface.
    /// </summary>
    private readonly short[] destination12 = new short[Width * Height];

    /// <summary>
    /// Measures dense eight-bit lossless reconstruction across a padded 1920-by-1088 frame.
    /// </summary>
    /// <returns>The final reconstructed sample, keeping the frame output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("8Bit")]
    public byte Reconstruct8BitFrame()
    {
        for (int row = 0; row < Height; row += TransformSize)
        {
            for (int column = 0; column < Width; column += TransformSize)
            {
                Av1InverseTransformer.Reconstruct8Bit(
                    this.coefficients,
                    this.destination8.AsSpan((row * Width) + column),
                    Width,
                    Av1TransformSize.Size4x4,
                    Av1TransformType.DctDct,
                    0,
                    this.coefficients.Length,
                    true,
                    this.workspace);
            }
        }

        return this.destination8[^1];
    }

    /// <summary>
    /// Measures dense twelve-bit lossless reconstruction across a padded 1920-by-1088 frame.
    /// </summary>
    /// <returns>The final reconstructed sample, keeping the frame output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("12Bit")]
    public short Reconstruct12BitFrame()
    {
        for (int row = 0; row < Height; row += TransformSize)
        {
            for (int column = 0; column < Width; column += TransformSize)
            {
                Av1InverseTransformer.ReconstructHighBitDepth(
                    this.coefficients,
                    this.destination12.AsSpan((row * Width) + column),
                    Width,
                    Av1TransformSize.Size4x4,
                    Av1TransformType.DctDct,
                    0,
                    this.coefficients.Length,
                    true,
                    Av1BitDepth.TwelveBit,
                    this.workspace);
            }
        }

        return this.destination12[^1];
    }

    /// <summary>
    /// Configures production-process measurements for hardware and scalar reconstruction.
    /// </summary>
    public sealed class Configuration : ManualConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
        {
            this.AddJob(Job.ShortRun.WithId("Hardware").AsBaseline());

            this.AddJob(
                Job.ShortRun
                    .WithId("Scalar")
                    .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0"));
        }
    }
}
