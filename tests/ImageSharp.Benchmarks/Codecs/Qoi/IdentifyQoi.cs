// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Qoi;

[Config(typeof(Config.ShortMultiFramework))]
public class IdentifyQoi
{
    private byte[] qoiBytes;

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Qoi.TestCardRGBA, TestImages.Qoi.TestCard, TestImages.Qoi.QoiLogo, TestImages.Qoi.EdgeCase, TestImages.Png.Bike)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages() => this.qoiBytes ??= File.ReadAllBytes(this.TestImageFullPath);

    [Benchmark]
    public ImageInfo Identify()
    {
        using MemoryStream memoryStream = new(this.qoiBytes);
        return QoiDecoder.Instance.Identify(DecoderOptions.Default, memoryStream);
    }
}
