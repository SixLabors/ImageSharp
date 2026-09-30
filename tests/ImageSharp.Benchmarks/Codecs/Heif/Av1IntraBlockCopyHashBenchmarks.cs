// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the 2x2 intra-block-copy hash seeds of a 1080p luma plane at each available intrinsic tier, against
/// the per-sample packing that they replace.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1IntraBlockCopyHashBenchmarks
{
    private const int Width = 1920;
    private const int Height = 1080;

    private readonly byte[] samples = new byte[Width * Height];
    private readonly ushort[] wideSamples = new ushort[Width * Height];
    private readonly uint[] seeds = new uint[(Width - 1) * (Height - 1)];

    /// <summary>
    /// Populates deterministic eight- and ten-bit planes outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < this.samples.Length; i++)
        {
            int value = ((i % Width) * 3) + ((i / Width) * 5) + ((i * 7919) % 17);
            this.samples[i] = (byte)value;
            this.wideSamples[i] = (ushort)(value & 1023);
        }
    }

    /// <summary>
    /// Measures the eight-bit seeds.
    /// </summary>
    /// <returns>The final seed, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Byte")]
    public uint PackByte()
    {
        for (int y = 0; y < Height - 1; y++)
        {
            Av1IntraBlockCopySearchIndex.PackSeedRow<byte, Av1IntraSuperblockEncoder.ByteOperator>(
                this.samples.AsSpan(y * Width, Width),
                this.samples.AsSpan((y + 1) * Width, Width),
                this.seeds.AsSpan(y * (Width - 1), Width - 1));
        }

        return this.seeds[^1];
    }

    /// <summary>
    /// Measures the per-sample eight-bit packing that <see cref="PackByte"/> replaced.
    /// </summary>
    /// <returns>The final seed, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Byte")]
    public uint PackByteScalar()
    {
        for (int y = 0; y < Height - 1; y++)
        {
            ReadOnlySpan<byte> top = this.samples.AsSpan(y * Width, Width);
            ReadOnlySpan<byte> bottom = this.samples.AsSpan((y + 1) * Width, Width);
            Span<uint> row = this.seeds.AsSpan(y * (Width - 1), Width - 1);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = ((uint)top[x] << 24) | ((uint)top[x + 1] << 16) | ((uint)bottom[x] << 8) | bottom[x + 1];
            }
        }

        return this.seeds[^1];
    }

    /// <summary>
    /// Measures the high-bit-depth seeds.
    /// </summary>
    /// <returns>The final seed, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("UInt16")]
    public uint PackUInt16()
    {
        for (int y = 0; y < Height - 1; y++)
        {
            Av1IntraBlockCopySearchIndex.PackSeedRow<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(
                this.wideSamples.AsSpan(y * Width, Width),
                this.wideSamples.AsSpan((y + 1) * Width, Width),
                this.seeds.AsSpan(y * (Width - 1), Width - 1));
        }

        return this.seeds[^1];
    }

    /// <summary>
    /// Measures the per-sample high-bit-depth packing that <see cref="PackUInt16"/> replaced.
    /// </summary>
    /// <returns>The final seed, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("UInt16")]
    public uint PackUInt16Scalar()
    {
        for (int y = 0; y < Height - 1; y++)
        {
            ReadOnlySpan<ushort> top = this.wideSamples.AsSpan(y * Width, Width);
            ReadOnlySpan<ushort> bottom = this.wideSamples.AsSpan((y + 1) * Width, Width);
            Span<uint> row = this.seeds.AsSpan(y * (Width - 1), Width - 1);
            for (int x = 0; x < row.Length; x++)
            {
                uint p0 = top[x];
                uint p1 = top[x + 1];
                uint p2 = bottom[x];
                uint p3 = bottom[x + 1];
                row[x] = (((p0 ^ (p0 >> 8)) & 255) << 24) |
                    (((p1 ^ (p1 >> 8)) & 255) << 16) |
                    (((p2 ^ (p2 >> 8)) & 255) << 8) |
                    ((p3 ^ (p3 >> 8)) & 255);
            }
        }

        return this.seeds[^1];
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
                    .WithEnvironmentVariable("DOTNET_EnableAVX512", "0"));

            this.AddJob(
                Job.ShortRun
                    .WithId("Vector128")
                    .WithEnvironmentVariable("DOTNET_EnableAVX512", "0")
                    .WithEnvironmentVariable("DOTNET_EnableAVX2", "0"));

            this.AddJob(
                Job.ShortRun
                    .WithId("Scalar")
                    .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0"));
        }
    }
}
