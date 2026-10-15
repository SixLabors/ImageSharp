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

    [GlobalSetup]
    public void ReadImages()
        => this.jpegBytes ??= File.ReadAllBytes(this.TestImageFullPath);

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
Measured 2026-10-16

| Method     | TestImage                                   | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated  | Alloc Ratio |
|----------- |-------------------------------------------- |-----------:|------------:|----------:|------:|--------:|----------:|----------:|----------:|-----------:|------------:|
| **ImageSharp** | **Jpg/baseline/jpeg420exif.jpg**                |   **9.771 ms** |   **1.3728 ms** | **0.0753 ms** |  **0.59** |    **0.01** |   **15.6250** |   **15.6250** |   **15.6250** |   **19.44 KB** |        **7.44** |
| SkiaSharp  | Jpg/baseline/jpeg420exif.jpg                |  16.534 ms |   5.1957 ms | 0.2848 ms |  1.00 |    0.02 |         - |         - |         - |    2.61 KB |        1.00 |
|            |                                             |            |             |           |       |         |           |           |           |            |             |
| **ImageSharp** | **Jpg/baseline/Lake.jpg**                       |  **20.598 ms** |   **2.7481 ms** | **0.1506 ms** |  **2.69** |    **0.02** |   **31.2500** |   **31.2500** |   **31.2500** |  **202.91 KB** |       **69.61** |
| SkiaSharp  | Jpg/baseline/Lake.jpg                       |   7.648 ms |   0.6882 ms | 0.0377 ms |  1.00 |    0.01 |         - |         - |         - |    2.92 KB |        1.00 |
|            |                                             |            |             |           |       |         |           |           |           |            |             |
| **ImageSharp** | **Jpg/icc-profiles/issue_2723.jpg**             |  **31.567 ms** |   **5.0570 ms** | **0.2772 ms** |  **1.60** |    **0.02** |  **937.5000** |  **937.5000** |  **937.5000** | **5413.59 KB** |    **1,891.34** |
| SkiaSharp  | Jpg/icc-profiles/issue_2723.jpg             |  19.732 ms |   3.1759 ms | 0.1741 ms |  1.00 |    0.01 |         - |         - |         - |    2.86 KB |        1.00 |
|            |                                             |            |             |           |       |         |           |           |           |            |             |
| **ImageSharp** | **Jpg/icc-profiles/issue-129.jpg**              | **247.858 ms** |  **38.1231 ms** | **2.0897 ms** |  **1.73** |    **0.02** |  **333.3333** |  **333.3333** |  **333.3333** |  **5576.7 KB** |    **1,958.35** |
| SkiaSharp  | Jpg/icc-profiles/issue-129.jpg              | 143.155 ms |  20.0687 ms | 1.1000 ms |  1.00 |    0.01 |         - |         - |         - |    2.85 KB |        1.00 |
|            |                                             |            |             |           |       |         |           |           |           |            |             |
| **ImageSharp** | **Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg**    |   **4.986 ms** |   **0.0827 ms** | **0.0045 ms** |  **2.37** |    **0.08** |         **-** |         **-** |         **-** |  **128.57 KB** |       **46.16** |
| SkiaSharp  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg    |   2.105 ms |   1.5835 ms | 0.0868 ms |  1.00 |    0.05 |         - |         - |         - |    2.79 KB |        1.00 |
|            |                                             |            |             |           |       |         |           |           |           |            |             |
| **ImageSharp** | **Jpg/issues/Issue518-Bad-RST-Progressive.jpg** | **148.005 ms** |  **40.7527 ms** | **2.2338 ms** |  **0.99** |    **0.04** | **1000.0000** | **1000.0000** | **1000.0000** |   **87.53 KB** |       **30.74** |
| SkiaSharp  | Jpg/issues/Issue518-Bad-RST-Progressive.jpg | 150.396 ms | 122.0738 ms | 6.6913 ms |  1.00 |    0.05 |         - |         - |         - |    2.85 KB |        1.00 |
*/
