// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures AV1 film-grain application across full-HD-equivalent 4:2:0 component planes.
/// </summary>
[Config(typeof(Configuration))]
[MemoryDiagnoser(displayGenColumns: false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Av1FilmGrainBenchmarks
{
    /// <summary>
    /// The aligned full-HD luma width.
    /// </summary>
    private const int Width = 1920;

    /// <summary>
    /// The aligned full-HD luma height.
    /// </summary>
    private const int Height = 1088;

    /// <summary>
    /// The luma width and height of one selected grain block.
    /// </summary>
    private const int BlockSize = 32;

    /// <summary>
    /// The 4:2:0 chroma-plane width.
    /// </summary>
    private const int ChromaWidth = Width / 2;

    /// <summary>
    /// The 4:2:0 chroma-plane height.
    /// </summary>
    private const int ChromaHeight = Height / 2;

    /// <summary>
    /// The 4:2:0 chroma width and height of one selected grain block.
    /// </summary>
    private const int ChromaBlockSize = BlockSize / 2;

    /// <summary>
    /// The deterministic eight-bit luma plane.
    /// </summary>
    private readonly byte[] luma8 = new byte[Width * Height];

    /// <summary>
    /// The deterministic eight-bit first chroma plane.
    /// </summary>
    private readonly byte[] cb8 = new byte[ChromaWidth * ChromaHeight];

    /// <summary>
    /// The deterministic eight-bit second chroma plane.
    /// </summary>
    private readonly byte[] cr8 = new byte[ChromaWidth * ChromaHeight];

    /// <summary>
    /// The deterministic twelve-bit luma plane.
    /// </summary>
    private readonly ushort[] luma12 = new ushort[Width * Height];

    /// <summary>
    /// The deterministic twelve-bit first chroma plane.
    /// </summary>
    private readonly ushort[] cb12 = new ushort[ChromaWidth * ChromaHeight];

    /// <summary>
    /// The deterministic twelve-bit second chroma plane.
    /// </summary>
    private readonly ushort[] cr12 = new ushort[ChromaWidth * ChromaHeight];

    /// <summary>
    /// The expanded luma scaling function.
    /// </summary>
    private readonly int[] scalingY = new int[256];

    /// <summary>
    /// The expanded first chroma scaling function.
    /// </summary>
    private readonly int[] scalingCb = new int[256];

    /// <summary>
    /// The expanded second chroma scaling function.
    /// </summary>
    private readonly int[] scalingCr = new int[256];

    /// <summary>
    /// The selected luma grain block.
    /// </summary>
    private readonly int[] lumaGrain = new int[BlockSize * BlockSize];

    /// <summary>
    /// The selected first chroma grain block.
    /// </summary>
    private readonly int[] cbGrain = new int[ChromaBlockSize * ChromaBlockSize];

    /// <summary>
    /// The selected second chroma grain block.
    /// </summary>
    private readonly int[] crGrain = new int[ChromaBlockSize * ChromaBlockSize];

    /// <summary>
    /// The active grain parameters shared by both measured sample precisions.
    /// </summary>
    private readonly ObuFilmGrainParameters parameters = new()
    {
        NumYPoints = 2,
        ChromaScalingFromLuma = true,
        GrainScalingMinus8 = 3
    };

    /// <summary>
    /// Populates deterministic source planes and grain blocks outside the measured traversal.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int index = 0; index < this.luma8.Length; index++)
        {
            int value = ((index * 37) + 113) & byte.MaxValue;
            this.luma8[index] = (byte)value;
            this.luma12[index] = (ushort)(value << 4);
        }

        for (int index = 0; index < this.cb8.Length; index++)
        {
            int cb = ((index * 53) + 97) & byte.MaxValue;
            int cr = ((index * 71) + 41) & byte.MaxValue;
            this.cb8[index] = (byte)cb;
            this.cr8[index] = (byte)cr;
            this.cb12[index] = (ushort)(cb << 4);
            this.cr12[index] = (ushort)(cr << 4);
        }

        for (int index = 0; index < this.lumaGrain.Length; index++)
        {
            this.lumaGrain[index] = ((index * 29) & byte.MaxValue) - 128;
        }

        for (int index = 0; index < this.cbGrain.Length; index++)
        {
            this.cbGrain[index] = ((index * 43) & byte.MaxValue) - 128;
            this.crGrain[index] = ((index * 61) & byte.MaxValue) - 128;
        }

        // A zero scaling function keeps every invocation's source planes stable. The measured code still performs the
        // production lookup, interpolation, grain multiplication, clipping, and native sample packing for every lane.
        this.scalingY.AsSpan().Clear();
        this.scalingCb.AsSpan().Clear();
        this.scalingCr.AsSpan().Clear();
    }

    /// <summary>
    /// Applies eight-bit grain blocks across full-HD-equivalent 4:2:0 planes.
    /// </summary>
    /// <returns>The final luma sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("8Bit")]
    public byte Apply8Bit()
    {
        for (int y = 0; y < Height; y += BlockSize)
        {
            for (int x = 0; x < Width; x += BlockSize)
            {
                int lumaOffset = (y * Width) + x;
                int chromaOffset = ((y / 2) * ChromaWidth) + (x / 2);

                Av1FilmGrainNoise.Apply(
                    this.parameters,
                    this.scalingY,
                    this.scalingCb,
                    this.scalingCr,
                    this.luma8.AsSpan(lumaOffset),
                    this.cb8.AsSpan(chromaOffset),
                    this.cr8.AsSpan(chromaOffset),
                    Width,
                    ChromaWidth,
                    this.lumaGrain,
                    this.cbGrain,
                    this.crGrain,
                    BlockSize,
                    ChromaBlockSize,
                    BlockSize / 2,
                    BlockSize / 2,
                    8,
                    1,
                    1,
                    isMonochrome: false,
                    isIdentityMatrix: false);
            }
        }

        return this.luma8[^1];
    }

    /// <summary>
    /// Applies twelve-bit grain blocks across full-HD-equivalent 4:2:0 planes.
    /// </summary>
    /// <returns>The final luma sample, keeping the output observable.</returns>
    [Benchmark]
    [BenchmarkCategory("12Bit")]
    public ushort Apply12Bit()
    {
        for (int y = 0; y < Height; y += BlockSize)
        {
            for (int x = 0; x < Width; x += BlockSize)
            {
                int lumaOffset = (y * Width) + x;
                int chromaOffset = ((y / 2) * ChromaWidth) + (x / 2);

                Av1FilmGrainNoise.Apply(
                    this.parameters,
                    this.scalingY,
                    this.scalingCb,
                    this.scalingCr,
                    this.luma12.AsSpan(lumaOffset),
                    this.cb12.AsSpan(chromaOffset),
                    this.cr12.AsSpan(chromaOffset),
                    Width,
                    ChromaWidth,
                    this.lumaGrain,
                    this.cbGrain,
                    this.crGrain,
                    BlockSize,
                    ChromaBlockSize,
                    BlockSize / 2,
                    BlockSize / 2,
                    12,
                    1,
                    1,
                    isMonochrome: false,
                    isIdentityMatrix: false);
            }
        }

        return this.luma12[^1];
    }

    /// <summary>
    /// Configures production-process measurements for hardware, no-AVX, and scalar grain application.
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
                    .WithId("NoAvx")
                    .WithEnvironmentVariable("DOTNET_EnableAVX", "0"));

            this.AddJob(
                Job.ShortRun
                    .WithId("Scalar")
                    .WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0"));
        }
    }
}
