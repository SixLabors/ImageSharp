// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Tests;
using SkiaSharp;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Image-specific Jpeg benchmarks
/// </summary>
[Config(typeof(Config.Short))]
public class DecodeJpeg_ImageSpecific
{
    private byte[] jpegBytes;

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

#pragma warning disable SA1115
    [Params(
        TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr,
        TestImages.Jpeg.BenchmarkSuite.BadRstProgressive518_Large444YCbCr,

        // The scaled result for the large image "ExifGetString750Transform_Huge420YCbCr"
        // is almost the same as the result for Jpeg420Exif,
        // which proves that the execution time for the most common YCbCr 420 path scales linearly.
        // TestImages.Jpeg.BenchmarkSuite.ExifGetString750Transform_Huge420YCbCr,
        TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr,
        TestImages.Jpeg.ICC.AdobeRgb,
        TestImages.Jpeg.ICC.CMYK,
        TestImages.Jpeg.ICC.YCCK)]

    public string TestImage { get; set; }

    /// <summary>
    /// Loads the encoded input before measurement.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        this.jpegBytes ??= File.ReadAllBytes(this.TestImageFullPath);
    }

    [Benchmark]
    public SDSize SystemDrawing()
    {
        using MemoryStream memoryStream = new(this.jpegBytes);

        // Request embedded-profile handling and image-data validation through GDI+.
        using SDImage image = SDImage.FromStream(memoryStream, useEmbeddedColorManagement: true, validateImageData: true);
        return image.Size;
    }

    [Benchmark]
    public Size ImageSharp()
    {
        using MemoryStream memoryStream = new(this.jpegBytes);
        using Image image = Image.Load(new DecoderOptions { SkipMetadata = true }, memoryStream);
        return new Size(image.Width, image.Height);
    }

    /// <summary>
    /// Decodes all pixels and converts the embedded profile to sRGB with Skia.
    /// </summary>
    /// <returns>The decoded image size.</returns>
    [Benchmark(Baseline = true)]
    public Size SkiaSharp()
    {
        using MemoryStream memoryStream = new(this.jpegBytes);
        using SKCodec codec = SKCodec.Create(memoryStream);
        using SKColorSpace colorSpace = SKColorSpace.CreateSrgb();

        // An explicit sRGB destination requests color conversion instead of retaining the source profile's samples.
        SKImageInfo info = new(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul, colorSpace);
        using SKBitmap image = SKBitmap.Decode(codec, info);
        return new Size(image.Width, image.Height);
    }
}

/*
Measured 2026-10-17

| Method         | TestImage                                    | Mean         | Error       | StdDev      | Ratio   | RatioSD   | Gen0        | Gen1        | Gen2        | Allocated   | Alloc Ratio   |
| -------------- | -------------------------------------------- | -----------: | ----------: | ----------: | ------: | --------: | ----------: | ----------: | ----------: | ----------: | ------------: |
| SystemDrawing  | Jpg/baseline/jpeg420exif.jpg                 |     7.981 ms |   0.1054 ms |   0.0551 ms |    0.49 |      0.01 |           - |           - |           - |       395 B |          0.15 |
| ImageSharp     | Jpg/baseline/jpeg420exif.jpg                 |     9.605 ms |   0.2538 ms |   0.1679 ms |    0.58 |      0.01 |     15.6250 |     15.6250 |     15.6250 |     19905 B |          7.44 |
| SkiaSharp      | Jpg/baseline/jpeg420exif.jpg                 |    16.440 ms |   0.4100 ms |   0.2712 ms |    1.00 |      0.02 |           - |           - |           - |      2674 B |          1.00 |
|                |                                              |              |             |             |         |           |             |             |             |             |               |
| SystemDrawing  | Jpg/baseline/Lake.jpg                        |    12.873 ms |   0.2006 ms |   0.1194 ms |    1.70 |      0.03 |           - |           - |           - |       399 B |          0.13 |
| ImageSharp     | Jpg/baseline/Lake.jpg                        |     7.526 ms |   0.0572 ms |   0.0340 ms |    0.99 |      0.02 |      7.8125 |      7.8125 |      7.8125 |     27124 B |          9.09 |
| SkiaSharp      | Jpg/baseline/Lake.jpg                        |     7.585 ms |   0.1895 ms |   0.1253 ms |    1.00 |      0.02 |           - |           - |           - |      2985 B |          1.00 |
|                |                                              |              |             |             |         |           |             |             |             |             |               |
| SystemDrawing  | Jpg/icc-profiles/issue_2723.jpg              |    13.852 ms |   0.5514 ms |   0.2884 ms |    0.71 |      0.02 |           - |           - |           - |       399 B |          0.14 |
| ImageSharp     | Jpg/icc-profiles/issue_2723.jpg              |    17.699 ms |   0.4466 ms |   0.2954 ms |    0.91 |      0.02 |    937.5000 |    937.5000 |    937.5000 |   3646674 B |      1,244.17 |
| SkiaSharp      | Jpg/icc-profiles/issue_2723.jpg              |    19.518 ms |   0.2638 ms |   0.1745 ms |    1.00 |      0.01 |           - |           - |           - |      2931 B |          1.00 |
|                |                                              |              |             |             |         |           |             |             |             |             |               |
| SystemDrawing  | Jpg/icc-profiles/issue-129.jpg               |    58.790 ms |   2.9322 ms |   1.7449 ms |    0.41 |      0.01 |           - |           - |           - |       471 B |          0.16 |
| ImageSharp     | Jpg/icc-profiles/issue-129.jpg               |   157.216 ms |   8.2200 ms |   5.4370 ms |    1.10 |      0.04 |    250.0000 |    250.0000 |    250.0000 |   3440528 B |      1,179.88 |
| SkiaSharp      | Jpg/icc-profiles/issue-129.jpg               |   142.835 ms |   3.8164 ms |   1.9960 ms |    1.00 |      0.02 |           - |           - |           - |      2916 B |          1.00 |
|                |                                              |              |             |             |         |           |             |             |             |             |               |
| SystemDrawing  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     |     5.812 ms |   1.0796 ms |   0.7141 ms |    2.79 |      0.33 |           - |           - |           - |       395 B |          0.14 |
| ImageSharp     | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     |     1.685 ms |   0.0196 ms |   0.0129 ms |    0.81 |      0.01 |           - |           - |           - |     29333 B |         10.29 |
| SkiaSharp      | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     |     2.084 ms |   0.0367 ms |   0.0243 ms |    1.00 |      0.02 |           - |           - |           - |      2852 B |          1.00 |
|                |                                              |              |             |             |         |           |             |             |             |             |               |
| SystemDrawing  | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  |   231.770 ms |   3.4177 ms |   2.0338 ms |    1.55 |      0.02 |           - |           - |           - |       427 B |          0.15 |
| ImageSharp     | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  |   149.025 ms |   1.8008 ms |   1.0716 ms |    0.99 |      0.01 |   1000.0000 |   1000.0000 |   1000.0000 |     88960 B |         30.51 |
| SkiaSharp      | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  |   149.810 ms |   2.8577 ms |   1.8902 ms |    1.00 |      0.02 |           - |           - |           - |      2916 B |          1.00 |
*/
