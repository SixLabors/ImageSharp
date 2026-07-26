// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.PixelBlenders;

/// <summary>
/// Compares straight-alpha and associated-alpha bulk pixel blending.
/// </summary>
[Config(typeof(Config.Short))]
public class AssociatedAlphaPixelBlenderBenchmark
{
    private const int PixelCount = 1024;

    private readonly Rgba32[] unassociatedDestination = new Rgba32[PixelCount];
    private readonly Rgba32[] unassociatedBackground = new Rgba32[PixelCount];
    private readonly Rgba32[] unassociatedSource = new Rgba32[PixelCount];
    private readonly Rgba32P[] associatedDestination = new Rgba32P[PixelCount];
    private readonly Rgba32P[] associatedBackground = new Rgba32P[PixelCount];
    private readonly Rgba32P[] associatedSource = new Rgba32P[PixelCount];
    private readonly float[] amounts = new float[PixelCount];
    private readonly Vector4[] unassociatedWorkingBuffer = new Vector4[PixelCount * 3];
    private readonly Vector4[] associatedWorkingBuffer = new Vector4[PixelCount * 3];
    private PixelBlender<Rgba32> unassociatedBlender;
    private PixelBlender<Rgba32P> associatedBlender;

    /// <summary>
    /// Gets or sets the color-blending mode measured by the benchmark.
    /// </summary>
    [ParamsAllValues]
    public PixelColorBlendingMode ColorMode { get; set; }

    /// <summary>
    /// Gets or sets the alpha-composition mode measured by the benchmark.
    /// </summary>
    [ParamsAllValues]
    public PixelAlphaCompositionMode AlphaMode { get; set; }

    /// <summary>
    /// Initializes equivalent straight-alpha and associated-alpha rows.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.unassociatedBlender = PixelOperations<Rgba32>.Instance.GetPixelBlender(this.ColorMode, this.AlphaMode);
        this.associatedBlender = PixelOperations<Rgba32P>.Instance.GetPixelBlender(this.ColorMode, this.AlphaMode);

        Random random = new(42);

        for (int i = 0; i < PixelCount; i++)
        {
            Rgba32 background = new((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(64, 256));
            Rgba32 source = new((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(64, 256));

            this.unassociatedBackground[i] = background;
            this.unassociatedSource[i] = source;
            this.associatedBackground[i] = Rgba32P.FromRgba32(background);
            this.associatedSource[i] = Rgba32P.FromRgba32(source);
            this.amounts[i] = random.NextSingle();
        }
    }

    /// <summary>
    /// Blends one row stored with straight alpha.
    /// </summary>
    /// <returns>The last destination pixel.</returns>
    [Benchmark(Description = "Straight alpha", Baseline = true)]
    public Rgba32 BlendUnassociated()
    {
        this.unassociatedBlender.Blend<Rgba32>(
            Configuration.Default,
            this.unassociatedDestination,
            this.unassociatedBackground,
            this.unassociatedSource,
            this.amounts,
            this.unassociatedWorkingBuffer);

        return this.unassociatedDestination[^1];
    }

    /// <summary>
    /// Blends one row stored with associated alpha.
    /// </summary>
    /// <returns>The last destination pixel.</returns>
    [Benchmark(Description = "Associated alpha")]
    public Rgba32P BlendAssociated()
    {
        this.associatedBlender.Blend<Rgba32P>(
            Configuration.Default,
            this.associatedDestination,
            this.associatedBackground,
            this.associatedSource,
            this.amounts,
            this.associatedWorkingBuffer);

        return this.associatedDestination[^1];
    }
}
