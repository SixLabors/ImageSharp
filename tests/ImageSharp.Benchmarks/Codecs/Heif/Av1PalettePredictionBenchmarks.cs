// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures frame-wide AV1 palette reconstruction at each available intrinsic tier.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1PalettePredictionBenchmarks
{
    /// <summary>
    /// The coded frame width, which is an exact multiple of the maximum palette block side.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The coded frame height including the final padded coding-tree row for a 1080-line presentation.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The maximum AV1 palette block side in samples.
    /// </summary>
    private const int BlockSize = 64;

    /// <summary>
    /// The eight-entry 8-bit palette reused by each benchmark block.
    /// </summary>
    private readonly ushort[] palette8 = [3, 37, 71, 109, 143, 181, 217, 251];

    /// <summary>
    /// The eight-entry 12-bit palette reused by each benchmark block.
    /// </summary>
    private readonly ushort[] palette12 = [17, 509, 1001, 1493, 1985, 2477, 2969, 4095];

    /// <summary>
    /// The decoded color-index map for one maximum-size palette block.
    /// </summary>
    private Buffer2D<byte> colorIndexMapBuffer;

    /// <summary>
    /// The row-addressable view of <see cref="colorIndexMapBuffer"/>.
    /// </summary>
    private Buffer2DRegion<byte> colorIndexMap;

    /// <summary>
    /// The frame-wide 8-bit reconstruction surface.
    /// </summary>
    private readonly byte[] destination8 = new byte[Width * Height];

    /// <summary>
    /// The frame-wide 12-bit reconstruction surface.
    /// </summary>
    private readonly short[] destination12 = new short[Width * Height];

    /// <summary>
    /// Populates a deterministic, spatially varying color-index map outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.colorIndexMapBuffer = SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.Allocate2D<byte>(BlockSize, BlockSize);
        this.colorIndexMap = new Buffer2DRegion<byte>(this.colorIndexMapBuffer);
        for (int row = 0; row < BlockSize; row++)
        {
            Span<byte> colorIndexRow = this.colorIndexMap.DangerousGetRowSpan(row);
            for (int column = 0; column < BlockSize; column++)
            {
                colorIndexRow[column] = (byte)(((row * 5) + (column * 3)) & 7);
            }
        }
    }

    /// <summary>
    /// Releases the row-addressable color-index map after the benchmark run.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.colorIndexMapBuffer?.Dispose();

    /// <summary>
    /// Measures frame-wide 8-bit palette reconstruction.
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
                Av1PalettePredictor.Predict(this.palette8, this.colorIndexMap, this.destination8.AsSpan((row * Width) + column), Width, BlockSize, BlockSize);
            }
        }

        return this.destination8[^1];
    }

    /// <summary>
    /// Measures frame-wide 12-bit palette reconstruction.
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
                Av1PalettePredictor.Predict(this.palette12, this.colorIndexMap, this.destination12.AsSpan((row * Width) + column), Width, BlockSize, BlockSize);
            }
        }

        return this.destination12[^1];
    }

    /// <summary>
    /// Configures production-process measurements for hardware, forced Vector512, AVX2, Vector128, and scalar paths.
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
                    .WithId("Vector512")
                    .WithEnvironmentVariable("DOTNET_PreferredVectorBitWidth", "512")
                    .WithEnvironmentVariable("COMPlus_PreferredVectorBitWidth", "512"));

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
