// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Measures the three ICC stages separately with one real image row.
/// </summary>
[Config(typeof(Config.Short))]
public class IccStages
{
    private ColorProfileConverterExtensionsIcc.IccTransform transform;
    private Vector4[] source;
    private Vector4[] pcs;
    private Vector4[] target;
    private Vector4[] destination;

    /// <summary>
    /// Prepares stable inputs for each stage outside the measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr);
        ImageInfo info = Image.Identify(path);
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = info.Metadata.IccProfile,
            TargetIccProfile = CompactSrgbV4Profile.Profile
        });
        this.transform = converter.GetIccTransform();

        using Image<RgbaVector> image = Image.Load<RgbaVector>(new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert }, path);
        this.source = new Vector4[image.Width];
        PixelOperations<RgbaVector>.Instance.ToScaledVector4(Configuration.Default, image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0), this.source);
        this.pcs = new Vector4[this.source.Length];
        this.target = new Vector4[this.source.Length];
        this.destination = new Vector4[this.source.Length];
        this.transform.Source.Convert(this.source, this.pcs);
        this.transform.Pcs.Convert(this.pcs, this.target);
    }

    /// <summary>
    /// Measures the source profile's device-to-PCS transform.
    /// </summary>
    [Benchmark]
    public void Source() => this.transform.Source.Convert(this.source, this.destination);

    /// <summary>
    /// Measures the encoded PCS connection.
    /// </summary>
    [Benchmark]
    public void Pcs() => this.transform.Pcs.Convert(this.pcs, this.destination);

    /// <summary>
    /// Measures the target profile's PCS-to-device transform.
    /// </summary>
    [Benchmark]
    public void Target() => this.transform.Target.Convert(this.target, this.destination);
}
