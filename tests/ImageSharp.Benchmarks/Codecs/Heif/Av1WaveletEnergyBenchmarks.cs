// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the first-pass wavelet energy of every 8x8 block of a 1080p luma plane at each available intrinsic
/// tier. The scalar job runs the one-block operator that the vector tiers replace.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1WaveletEnergyBenchmarks
{
    private const int Width = 1920;
    private const int Height = 1088;

    private readonly byte[] samples = new byte[Width * Height];
    private readonly ushort[] wideSamples = new ushort[Width * Height];
    private readonly int[] energies = new int[Width / 8];

    /// <summary>
    /// Populates deterministic eight- and ten-bit planes outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < this.samples.Length; i++)
        {
            int value = ((i % Width) * 3) + ((i / Width) * 5) + ((i * 7919) % 23);
            this.samples[i] = (byte)value;
            this.wideSamples[i] = (ushort)(value & 1023);
        }
    }

    /// <summary>
    /// Measures the eight-bit energies.
    /// </summary>
    /// <returns>The final energy, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Byte")]
    public int Byte()
    {
        for (int row = 0; row < Height; row += 8)
        {
            Av1FirstPass<byte, Av1FirstPassOperator.ByteOperator>.GetWaveletEnergies(this.samples, row * Width, Width, this.energies);
        }

        return this.energies[^1];
    }

    /// <summary>
    /// Measures the high-bit-depth energies.
    /// </summary>
    /// <returns>The final energy, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("UInt16")]
    public int UInt16()
    {
        for (int row = 0; row < Height; row += 8)
        {
            Av1FirstPass<ushort, Av1FirstPassOperator.UInt16Operator>.GetWaveletEnergies(this.wideSamples, row * Width, Width, this.energies);
        }

        return this.energies[^1];
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
