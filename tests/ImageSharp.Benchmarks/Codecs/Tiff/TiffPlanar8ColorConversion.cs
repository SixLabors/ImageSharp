// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares scalar and bulk decoding of 8-bit RGB and RGBA planes.
/// </summary>
[Config(typeof(Config.Short))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TiffPlanar8ColorConversion
{
    private const int Length = 1024;

    private readonly RgbPlanarTiffColor<RgbaVector> rgbReader = new(Configuration.Default, new TiffBitsPerSample(8, 8, 8));
    private readonly RgbaPlanarTiffColor<RgbaVector> rgbaReader = new(
        Configuration.Default,
        TiffExtraSampleType.UnassociatedAlphaData,
        new TiffBitsPerSample(8, 8, 8, 8));

    private readonly Image<RgbaVector> rgbDestination = new(Length, 1);
    private readonly Image<RgbaVector> rgbaDestination = new(Length, 1);
    private IMemoryOwner<byte>[] rgbPlanes = [];
    private IMemoryOwner<byte>[] rgbaPlanes = [];

    /// <summary>
    /// Initializes identical encoded plane bytes outside the measured methods.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.rgbPlanes = new IMemoryOwner<byte>[3];
        this.rgbaPlanes = new IMemoryOwner<byte>[4];
        Random random = new(42);

        for (int channel = 0; channel < this.rgbaPlanes.Length; channel++)
        {
            IMemoryOwner<byte> plane = Configuration.Default.MemoryAllocator.Allocate<byte>(Length);
            this.rgbaPlanes[channel] = plane;
            if (channel < this.rgbPlanes.Length)
            {
                this.rgbPlanes[channel] = plane;
            }

            // Each channel owns one byte per pixel; a fixed seed reproduces the same planes for both paths.
            random.NextBytes(plane.GetSpan());
        }
    }

    /// <summary>
    /// Releases the benchmark planes and destinations.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (IMemoryOwner<byte> plane in this.rgbaPlanes)
        {
            plane.Dispose();
        }

        this.rgbDestination.Dispose();
        this.rgbaDestination.Dispose();
    }

    /// <summary>
    /// Measures scalar decoding and packing of 8-bit RGB planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGB8")]
    public RgbaVector RgbScalar()
    {
        Span<byte> red = this.rgbPlanes[0].GetSpan();
        Span<byte> green = this.rgbPlanes[1].GetSpan();
        Span<byte> blue = this.rgbPlanes[2].GetSpan();
        Span<RgbaVector> row = this.rgbDestination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int x = 0; x < Length; x++)
        {
            // Each plane contributes one unsigned byte; 255 maps its channel to full intensity.
            row[x] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(red[x] / 255F, green[x] / 255F, blue[x] / 255F, 1F));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk decoding and packing of 8-bit RGB planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGB8")]
    public RgbaVector RgbBulk()
    {
        this.rgbReader.Decode(this.rgbPlanes, this.rgbDestination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.rgbDestination[Length - 1, 0];
    }

    /// <summary>
    /// Measures scalar decoding and packing of 8-bit RGBA planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RGBA8")]
    public RgbaVector RgbaScalar()
    {
        Span<byte> red = this.rgbaPlanes[0].GetSpan();
        Span<byte> green = this.rgbaPlanes[1].GetSpan();
        Span<byte> blue = this.rgbaPlanes[2].GetSpan();
        Span<byte> alpha = this.rgbaPlanes[3].GetSpan();
        Span<RgbaVector> row = this.rgbaDestination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);

        for (int x = 0; x < Length; x++)
        {
            // Divide straight-alpha RGB and alpha codes by 255, as the bulk decoder does.
            row[x] = RgbaVector.FromUnassociatedScaledVector4(new Vector4(red[x] / 255F, green[x] / 255F, blue[x] / 255F, alpha[x] / 255F));
        }

        return row[^1];
    }

    /// <summary>
    /// Measures bulk decoding and packing of 8-bit RGBA planes.
    /// </summary>
    /// <returns>The last converted pixel.</returns>
    [Benchmark]
    [BenchmarkCategory("RGBA8")]
    public RgbaVector RgbaBulk()
    {
        this.rgbaReader.Decode(this.rgbaPlanes, this.rgbaDestination.Frames.RootFrame.PixelBuffer, 0, 0, Length, 1);
        return this.rgbaDestination[Length - 1, 0];
    }
}
