// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#if OS_WINDOWS
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// An expensive Jpeg benchmark, running on a wide range of input images,
/// showing aggregate results.
/// </summary>
[Config(typeof(Config.Short))]
public class DecodeJpeg_Aggregate : MultiImageBenchmarkBase
{
    protected override IEnumerable<string> InputImageSubfoldersOrFiles
        =>
        [
            TestImages.Jpeg.BenchmarkSuite.Jpeg400_SmallMonochrome,
            TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr,
            TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr,
            TestImages.Jpeg.BenchmarkSuite.MissingFF00ProgressiveBedroom159_MidSize420YCbCr,
            TestImages.Jpeg.BenchmarkSuite.ExifGetString750Transform_Huge420YCbCr,
        ];

    [Params(InputImageCategory.AllImages)]
    public override InputImageCategory InputCategory { get; set; }

    [Benchmark]
    public void ImageSharp()
        => this.ForEachStream(Image.Load<Rgba32>);

    [Benchmark(Baseline = true)]
    public void SystemDrawing()
        => this.ForEachStream(SDImage.FromStream);
}
#endif
