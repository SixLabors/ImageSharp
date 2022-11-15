// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.ShortMultiFramework))]
public class IdentifyJpeg
{
    private byte[] jpegBytes;

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Jpeg.Baseline.Jpeg420Exif, TestImages.Jpeg.Baseline.Calliphora)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages() => this.jpegBytes ??= File.ReadAllBytes(this.TestImageFullPath);

    [Benchmark]
    public IImageInfo Identify()
    {
        using MemoryStream memoryStream = new(this.jpegBytes);
        JpegDecoder decoder = new();
        return decoder.Identify(DecoderOptions.Default, memoryStream);
    }
}
