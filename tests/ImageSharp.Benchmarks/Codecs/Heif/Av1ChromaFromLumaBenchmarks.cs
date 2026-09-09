// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures frame-wide AV1 chroma-from-luma prediction at each available intrinsic tier.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1ChromaFromLumaBenchmarks
{
    /// <summary>
    /// The coded frame width, which is an exact multiple of the maximum CfL block side.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The coded frame height including the final padded coding-tree row for a 1080-line presentation.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The maximum CfL block side in chroma samples.
    /// </summary>
    private const int BlockSize = 32;

    /// <summary>
    /// The fixed-stride Q3 luma residual surface reused by each benchmark block.
    /// </summary>
    private readonly short[] lumaQ3 = new short[BlockSize * BlockSize];

    /// <summary>
    /// The frame-wide 8-bit DC prediction surface.
    /// </summary>
    private readonly byte[] destination8 = new byte[Width * Height];

    /// <summary>
    /// The frame-wide 12-bit DC prediction surface.
    /// </summary>
    private readonly short[] destination12 = new short[Width * Height];

    /// <summary>
    /// Populates deterministic Q3 residuals and DC predictions outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int index = 0; index < this.lumaQ3.Length; index++)
        {
            this.lumaQ3[index] = (short)(((index * 4051) % 65521) - 32760);
        }

        this.destination8.AsSpan().Fill(137);
        this.destination12.AsSpan().Fill(2101);
    }

    /// <summary>
    /// Measures frame-wide 8-bit chroma-from-luma prediction.
    /// </summary>
    /// <returns>The final reconstructed sample, keeping the frame output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("8Bit")]
    public byte Predict8BitFrame()
    {
        for (int row = 0; row < Height; row += BlockSize)
        {
            for (int column = 0; column < Width; column += BlockSize)
            {
                Av1ChromaFromLumaPredictor.Predict(this.lumaQ3, this.destination8.AsSpan((row * Width) + column), Width, 11, BlockSize, BlockSize);
            }
        }

        return this.destination8[^1];
    }

    /// <summary>
    /// Measures frame-wide 12-bit chroma-from-luma prediction.
    /// </summary>
    /// <returns>The final reconstructed sample, keeping the frame output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("12Bit")]
    public short Predict12BitFrame()
    {
        for (int row = 0; row < Height; row += BlockSize)
        {
            for (int column = 0; column < Width; column += BlockSize)
            {
                Av1ChromaFromLumaPredictor.Predict(this.lumaQ3, this.destination12.AsSpan((row * Width) + column), Width, 11, 12, BlockSize, BlockSize);
            }
        }

        return this.destination12[^1];
    }

    /// <summary>
    /// Configures production-process measurements for the default, AVX2, Vector128, and scalar paths.
    /// </summary>
    public sealed class Configuration : ManualConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
        {
            this.AddJob(
                Job.ShortRun
                    .WithId("Hardware")
                    .AsBaseline());

            this.AddJob(
                Job.ShortRun
                    .WithId("Avx2")
                    .WithEnvironmentVariable("DOTNET_EnableAVX512F", "0"));

            this.AddJob(
                Job.ShortRun
                    .WithId("Vector128")
                    .WithEnvironmentVariable("DOTNET_EnableAVX512F", "0")
                    .WithEnvironmentVariable("DOTNET_EnableAVX2", "0"));

            this.AddJob(
                Job.ShortRun
                    .WithId("Scalar")
                    .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0"));
        }
    }
}
