// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

/// <summary>
/// Measures planar RGB packing for padded and exact-length decoder rows.
/// </summary>
public class PixelConversion_PackFromRgbPlanesExact
{
    private byte[] red;
    private byte[] green;
    private byte[] blue;
    private Rgb24[] exactDestination;
    private Rgb24[] paddedDestination;

    /// <summary>
    /// Gets or sets the decoded row width.
    /// </summary>
    [Params(1920, 4242)]
    public int Count { get; set; }

    /// <summary>
    /// Creates deterministic component planes and destination rows outside the measured operation.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.red = new byte[this.Count];
        this.green = new byte[this.Count];
        this.blue = new byte[this.Count];
        this.exactDestination = new Rgb24[this.Count];
        this.paddedDestination = new Rgb24[this.Count + 3];

        new Random(42).NextBytes(this.red);
        new Random(43).NextBytes(this.green);
        new Random(44).NextBytes(this.blue);
    }

    /// <summary>
    /// Packs a row using the legacy decoder contract with three writable destination pixels beyond the row.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void PaddedDestination() => SimdUtils.PackFromRgbPlanes(this.red, this.green, this.blue, this.paddedDestination);

    /// <summary>
    /// Packs through a padded proxy and copies the completed row, matching the former decoder fallback.
    /// </summary>
    [Benchmark]
    public void PaddedProxyAndCopy()
    {
        SimdUtils.PackFromRgbPlanes(this.red, this.green, this.blue, this.paddedDestination);
        this.paddedDestination.AsSpan(0, this.Count).CopyTo(this.exactDestination);
    }

    /// <summary>
    /// Packs a row directly into the exact-length destination exposed by an image frame.
    /// </summary>
    [Benchmark]
    public void ExactDestination() => SimdUtils.PackFromRgbPlanes(this.red, this.green, this.blue, this.exactDestination);
}
