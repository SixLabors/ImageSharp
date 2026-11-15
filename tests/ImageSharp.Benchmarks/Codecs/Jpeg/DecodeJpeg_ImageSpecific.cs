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

    // All three libraries read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream inputStream;

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
        this.inputStream ??= new MemoryStream(this.jpegBytes, false);
    }

    /// <summary>
    /// Releases the shared input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
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
        this.inputStream.Position = 0;

        // GDI+ can retain native CMYK despite this request. The issue-129.jpg result is annotated below.
        using SDImage image = SDImage.FromStream(this.inputStream, useEmbeddedColorManagement: true, validateImageData: true);
        return image.Size;
    }

    [Benchmark]
    public Size ImageSharp()
    {
        this.inputStream.Position = 0;
        using Image image = Image.Load(new DecoderOptions(Configuration.Default) { SkipMetadata = true }, this.inputStream);
        return new Size(image.Width, image.Height);
    }

    /// <summary>
    /// Decodes all pixels and converts the embedded profile to sRGB with Skia.
    /// </summary>
    /// <returns>The decoded image size.</returns>
    [Benchmark(Baseline = true)]
    public Size SkiaSharp()
    {
        this.inputStream.Position = 0;

        // SKCodec.Create(Stream) disposes the stream with the codec. This wrapper keeps the shared stream open.
        using SKCodec codec = SKCodec.Create(new SKManagedStream(this.inputStream, false));
        using SKColorSpace colorSpace = SKColorSpace.CreateSrgb();

        // An explicit sRGB destination requests color conversion instead of retaining the source profile's samples.
        SKImageInfo info = new(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul, colorSpace);
        using SKBitmap image = SKBitmap.Decode(codec, info);
        return new Size(image.Width, image.Height);
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method        | TestImage                                   | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
    |-------------- |-------------------------------------------- |-----------:|-----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
    | SystemDrawing | Jpg/baseline/jpeg420exif.jpg                |   7.794 ms |  0.9548 ms | 0.0523 ms |  0.53 |    0.00 |         - |         - |         - |     331 B |        0.13 |
    | ImageSharp    | Jpg/baseline/jpeg420exif.jpg                |   5.758 ms |  0.4221 ms | 0.0231 ms |  0.39 |    0.00 |    7.8125 |    7.8125 |    7.8125 |   18441 B |        7.13 |
    | SkiaSharp     | Jpg/baseline/jpeg420exif.jpg                |  14.634 ms |  0.3240 ms | 0.0178 ms |  1.00 |    0.00 |         - |         - |         - |    2585 B |        1.00 |
    |               |                                             |            |            |           |       |         |           |           |           |           |             |
    | SystemDrawing | Jpg/baseline/Lake.jpg                       |  12.413 ms |  0.3332 ms | 0.0183 ms |  1.64 |    0.00 |         - |         - |         - |     335 B |        0.12 |
    | ImageSharp    | Jpg/baseline/Lake.jpg                       |   4.854 ms |  0.0921 ms | 0.0050 ms |  0.64 |    0.00 |         - |         - |         - |   25198 B |        8.65 |
    | SkiaSharp     | Jpg/baseline/Lake.jpg                       |   7.583 ms |  0.1736 ms | 0.0095 ms |  1.00 |    0.00 |         - |         - |         - |    2913 B |        1.00 |
    |               |                                             |            |            |           |       |         |           |           |           |           |             |
    | SystemDrawing | Jpg/icc-profiles/issue_2723.jpg             |  13.112 ms |  1.5200 ms | 0.0833 ms |  0.66 |    0.00 |         - |         - |         - |     335 B |        0.12 |
    | ImageSharp    | Jpg/icc-profiles/issue_2723.jpg             |  16.552 ms |  2.4033 ms | 0.1317 ms |  0.83 |    0.01 |  937.5000 |  937.5000 |  937.5000 | 3646747 B |    1,275.53 |
    | SkiaSharp     | Jpg/icc-profiles/issue_2723.jpg             |  19.904 ms |  0.3348 ms | 0.0184 ms |  1.00 |    0.00 |         - |         - |         - |    2859 B |        1.00 |
    |               |                                             |            |            |           |       |         |           |           |           |           |             |
    | SystemDrawing | Jpg/icc-profiles/issue-129.jpg              |  56.193 ms |  6.3019 ms | 0.3454 ms |  0.38 |    0.00 |         - |         - |         - |     407 B |        0.14 |
    | ImageSharp    | Jpg/icc-profiles/issue-129.jpg              | 126.909 ms | 18.9620 ms | 1.0394 ms |  0.85 |    0.01 |  200.0000 |  200.0000 |  200.0000 | 3430176 B |    1,206.11 |
    | SkiaSharp     | Jpg/icc-profiles/issue-129.jpg              | 148.437 ms |  1.2990 ms | 0.0712 ms |  1.00 |    0.00 |         - |         - |         - |    2844 B |        1.00 |
    |               |                                             |            |            |           |       |         |           |           |           |           |             |
    | SystemDrawing | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg    |   4.942 ms |  1.5762 ms | 0.0864 ms |  2.28 |    0.05 |         - |         - |         - |     331 B |        0.12 |
    | ImageSharp    | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg    |   1.235 ms |  0.0711 ms | 0.0039 ms |  0.57 |    0.01 |         - |         - |         - |   27445 B |        9.87 |
    | SkiaSharp     | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg    |   2.166 ms |  0.7905 ms | 0.0433 ms |  1.00 |    0.02 |         - |         - |         - |    2780 B |        1.00 |
    |               |                                             |            |            |           |       |         |           |           |           |           |             |
    | SystemDrawing | Jpg/issues/Issue518-Bad-RST-Progressive.jpg | 221.317 ms |  1.7345 ms | 0.0951 ms |  1.44 |    0.01 |         - |         - |         - |     539 B |        0.19 |
    | ImageSharp    | Jpg/issues/Issue518-Bad-RST-Progressive.jpg | 124.429 ms | 13.7442 ms | 0.7534 ms |  0.81 |    0.01 | 1000.0000 | 1000.0000 | 1000.0000 |  112146 B |       39.43 |
    | SkiaSharp     | Jpg/issues/Issue518-Bad-RST-Progressive.jpg | 153.279 ms | 32.1379 ms | 1.7616 ms |  1.00 |    0.01 |         - |         - |         - |    2844 B |        1.00 |

    Allocated reports managed memory only.
    SystemDrawing returns native CMYK for issue-129.jpg. Its timing excludes the embedded CMYK-to-sRGB conversion
    performed by ImageSharp and Skia. The existing GDI+ comparison found identical rendered pixels with ICC enabled,
    disabled, removed or replaced. The Adobe RGB control changed when ICC was enabled.
    */
}
