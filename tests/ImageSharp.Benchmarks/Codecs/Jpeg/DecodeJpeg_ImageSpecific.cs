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

    /// <summary>
    /// Loads and validates the image through GDI+ with embedded color management requested.
    /// </summary>
    /// <remarks>
    /// For issue-129.jpg, GDI+ retains native CMYK samples and does not apply the embedded CMYK profile.
    /// Its timing excludes the CMYK-to-sRGB conversion performed by ImageSharp and Skia. See table note [1].
    /// </remarks>
    /// <returns>The loaded image size.</returns>
    [Benchmark]
    public SDSize SystemDrawing()
    {
        using MemoryStream memoryStream = new(this.jpegBytes);

        // GDI+ can retain native CMYK despite this request. The issue-129.jpg result is annotated below.
        using SDImage image = SDImage.FromStream(memoryStream, useEmbeddedColorManagement: true, validateImageData: true);
        return image.Size;
    }

    [Benchmark]
    public Size ImageSharp()
    {
        using MemoryStream memoryStream = new(this.jpegBytes);
        using Image image = Image.Load(new DecoderOptions(Configuration.Default) { SkipMetadata = true }, memoryStream);
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

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method         | TestImage                                    |         Mean |        Error |      StdDev |   Ratio |   RatioSD |       Gen0 |       Gen1 |       Gen2 |   Allocated |   Alloc Ratio |
    | -------------- | -------------------------------------------- | -----------: | -----------: | ----------: | ------: | --------: | ---------: | ---------: | ---------: | ----------: | ------------: |
    | SystemDrawing  | Jpg/baseline/jpeg420exif.jpg                 |     7.704 ms |    0.8684 ms |   0.0476 ms |    0.49 |      0.00 |          - |          - |          - |       395 B |          0.15 |
    | ImageSharp     | Jpg/baseline/jpeg420exif.jpg                 |     9.357 ms |    0.3325 ms |   0.0182 ms |    0.60 |      0.00 |          - |          - |          - |     16320 B |          6.18 |
    | SkiaSharp      | Jpg/baseline/jpeg420exif.jpg                 |    15.714 ms |    0.9678 ms |   0.0530 ms |    1.00 |      0.00 |          - |          - |          - |      2640 B |          1.00 |
    |                |                                              |              |              |             |         |           |            |            |            |             |               |
    | SystemDrawing  | Jpg/baseline/Lake.jpg                        |    12.424 ms |    0.4965 ms |   0.0272 ms |    1.73 |      0.01 |          - |          - |          - |       399 B |          0.14 |
    | ImageSharp     | Jpg/baseline/Lake.jpg                        |     8.618 ms |    0.3989 ms |   0.0219 ms |    1.20 |      0.00 |          - |          - |          - |     26033 B |          9.14 |
    | SkiaSharp      | Jpg/baseline/Lake.jpg                        |     7.162 ms |    0.3309 ms |   0.0181 ms |    1.00 |      0.00 |          - |          - |          - |      2848 B |          1.00 |
    |                |                                              |              |              |             |         |           |            |            |            |             |               |
    | SystemDrawing  | Jpg/icc-profiles/issue_2723.jpg              |    13.057 ms |    1.2113 ms |   0.0664 ms |    0.70 |      0.00 |          - |          - |          - |       399 B |          0.15 |
    | ImageSharp     | Jpg/icc-profiles/issue_2723.jpg              |    20.457 ms |    2.8332 ms |   0.1553 ms |    1.09 |      0.01 |   968.7500 |   968.7500 |   968.7500 |   3631815 B |      1,375.69 |
    | SkiaSharp      | Jpg/icc-profiles/issue_2723.jpg              |    18.783 ms |    2.0060 ms |   0.1100 ms |    1.00 |      0.01 |          - |          - |          - |      2640 B |          1.00 |
    |                |                                              |              |              |             |         |           |            |            |            |             |               |
    | SystemDrawing  | Jpg/icc-profiles/issue-129.jpg               |    54.629 ms |    7.8786 ms |   0.4319 ms |    0.40 |      0.00 |          - |          - |          - |       459 B |          0.17 |
    | ImageSharp     | Jpg/icc-profiles/issue-129.jpg               |   155.386 ms |   26.1410 ms |   1.4329 ms |    1.13 |      0.01 |          - |          - |          - |   3386594 B |      1,282.80 |
    | SkiaSharp      | Jpg/icc-profiles/issue-129.jpg               |   137.003 ms |    7.6874 ms |   0.4214 ms |    1.00 |      0.00 |          - |          - |          - |      2640 B |          1.00 |
    |                |                                              |              |              |             |         |           |            |            |            |             |               |
    | SystemDrawing  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     |     5.180 ms |    2.6513 ms |   0.1453 ms |    2.60 |      0.07 |          - |          - |          - |       395 B |          0.14 |
    | ImageSharp     | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     |     2.034 ms |    0.1005 ms |   0.0055 ms |    1.02 |      0.01 |          - |          - |          - |     26410 B |          9.27 |
    | SkiaSharp      | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     |     1.990 ms |    0.3455 ms |   0.0189 ms |    1.00 |      0.01 |          - |          - |          - |      2848 B |          1.00 |
    |                |                                              |              |              |             |         |           |            |            |            |             |               |
    | SystemDrawing  | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  |   226.616 ms |   16.2417 ms |   0.8903 ms |    1.60 |      0.01 |          - |          - |          - |       603 B |          0.23 |
    | ImageSharp     | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  |   143.563 ms |   36.5600 ms |   2.0040 ms |    1.01 |      0.01 |          - |          - |          - |     47994 B |         18.18 |
    | SkiaSharp      | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  |   142.046 ms |    4.5723 ms |   0.2506 ms |    1.00 |      0.00 |          - |          - |          - |      2640 B |          1.00 |

    Allocated reports managed memory only.
    SystemDrawing returns native CMYK for issue-129.jpg. Its timing excludes the embedded CMYK-to-sRGB conversion
    performed by ImageSharp and Skia. The existing GDI+ comparison found identical rendered pixels with ICC enabled,
    disabled, removed or replaced. The Adobe RGB control changed when ICC was enabled.
    */
}
