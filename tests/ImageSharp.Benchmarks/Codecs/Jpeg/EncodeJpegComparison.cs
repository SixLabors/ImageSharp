// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SkiaSharp;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Benchmark for performance comparison between other codecs.
/// </summary>
/// <remarks>
/// This benchmarks tests baseline 4:2:0 chroma sampling path.
/// </remarks>
[Config(typeof(Config.Short))]
public class EncodeJpegComparison
{
    private MemoryStream destinationStream;

    // ImageSharp
    private Image<Rgba32> imageImageSharp;
    private JpegEncoder encoderImageSharp;

    // SkiaSharp
    private SKBitmap imageSkiaSharp;

    /// <summary>
    /// Gets or sets the input decoded to supply identical sRGB pixels to both encoders.
    /// </summary>
    [Params(
        TestImages.Jpeg.Baseline.Calliphora,
        TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr,
        TestImages.Jpeg.BenchmarkSuite.BadRstProgressive518_Large444YCbCr,
        TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr,
        TestImages.Jpeg.ICC.AdobeRgb,
        TestImages.Jpeg.ICC.CMYK,
        TestImages.Jpeg.ICC.YCCK)]
    public string TestImage { get; set; }

    /// <summary>
    /// Gets or sets the quality passed to both JPEG encoders.
    /// </summary>
    [Params(75, 90, 100)]
    public int Quality { get; set; }

    /// <summary>
    /// Prepares identical pixels and a reusable output stream outside the measured encoding operation.
    /// </summary>
    [GlobalSetup]
    public unsafe void Setup()
    {
        using FileStream imageBinaryStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage));
        DecoderOptions options = new(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert };
        this.imageImageSharp = Image.Load<Rgba32>(options, imageBinaryStream);

        // Skia encodes these bitmap pixels without copying the original JPEG metadata. Match that work in ImageSharp.
        this.encoderImageSharp = new JpegEncoder { Quality = this.Quality, ColorType = JpegColorType.YCbCrRatio420, SkipMetadata = true };

        // Decode once so differences between reference decoders cannot change the pixels each encoder receives.
        // Rgba8888 with unassociated alpha has the same byte layout as Rgba32; the JPEG inputs are opaque.
        SKImageInfo info = new(this.imageImageSharp.Width, this.imageImageSharp.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        this.imageSkiaSharp = new SKBitmap(info);

        // The bitmap owns this writable native allocation for the lifetime of the benchmark case.
        Span<byte> skiaPixels = new(this.imageSkiaSharp.GetPixels().ToPointer(), this.imageSkiaSharp.ByteCount);
        this.imageImageSharp.CopyPixelDataTo(skiaPixels);

        this.destinationStream = new MemoryStream();
    }

    /// <summary>
    /// Releases both pixel buffers and the output stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.imageImageSharp.Dispose();
        this.imageImageSharp = null;

        this.imageSkiaSharp.Dispose();
        this.imageSkiaSharp = null;

        this.destinationStream.Dispose();
        this.destinationStream = null;
    }

    [Benchmark(Description = "ImageSharp")]
    public void BenchmarkImageSharp()
    {
        this.imageImageSharp.SaveAsJpeg(this.destinationStream, this.encoderImageSharp);
        this.destinationStream.Seek(0, SeekOrigin.Begin);
    }

    [Benchmark(Baseline = true, Description = "SkiaSharp")]
    public void BenchmarkSkiaSharp()
    {
        // Skia owns the encoded native buffer. Release it after copying the result to the stream.
        using SKData encoded = this.imageSkiaSharp.Encode(SKEncodedImageFormat.Jpeg, this.Quality);
        encoded.SaveTo(this.destinationStream);
        this.destinationStream.Seek(0, SeekOrigin.Begin);
    }

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method      | TestImage                                    | Quality  |         Mean |        Error |      StdDev |   Ratio |       Gen0 |       Gen1 |       Gen2 |   Allocated |   Alloc Ratio |
    | ----------- | -------------------------------------------- | -------- | -----------: | -----------: | ----------: | ------: | ---------: | ---------: | ---------: | ----------: | ------------: |
    | ImageSharp  | Jpg/baseline/Calliphora.jpg                  | 75       |     4.273 ms |    0.5526 ms |   0.0303 ms |    0.40 |          - |          - |          - |     15488 B |         48.40 |
    | SkiaSharp   | Jpg/baseline/Calliphora.jpg                  | 75       |    10.788 ms |    0.5170 ms |   0.0283 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/baseline/Calliphora.jpg                  | 90       |     4.514 ms |    0.0447 ms |   0.0025 ms |    0.42 |          - |          - |          - |     15488 B |         48.40 |
    | SkiaSharp   | Jpg/baseline/Calliphora.jpg                  | 90       |    10.807 ms |    1.0280 ms |   0.0564 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/baseline/Calliphora.jpg                  | 100      |     6.406 ms |    0.5030 ms |   0.0276 ms |    0.45 |          - |          - |          - |     15488 B |         48.40 |
    | SkiaSharp   | Jpg/baseline/Calliphora.jpg                  | 100      |    14.223 ms |    0.3997 ms |   0.0219 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/baseline/jpeg420exif.jpg                 | 75       |    11.384 ms |    0.3547 ms |   0.0194 ms |    0.35 |          - |          - |          - |     23440 B |         73.25 |
    | SkiaSharp   | Jpg/baseline/jpeg420exif.jpg                 | 75       |    32.790 ms |    5.1040 ms |   0.2798 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/baseline/jpeg420exif.jpg                 | 90       |    14.719 ms |    2.8007 ms |   0.1535 ms |    0.40 |          - |          - |          - |     23440 B |         73.25 |
    | SkiaSharp   | Jpg/baseline/jpeg420exif.jpg                 | 90       |    36.957 ms |    1.1867 ms |   0.0650 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/baseline/jpeg420exif.jpg                 | 100      |    17.215 ms |    3.0478 ms |   0.1671 ms |    0.37 |          - |          - |          - |     31123 B |         96.06 |
    | SkiaSharp   | Jpg/baseline/jpeg420exif.jpg                 | 100      |    46.915 ms |    7.4895 ms |   0.4105 ms |    1.00 |          - |          - |          - |       324 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/baseline/Lake.jpg                        | 75       |     4.445 ms |    0.9934 ms |   0.0545 ms |    0.43 |          - |          - |          - |     23112 B |         72.22 |
    | SkiaSharp   | Jpg/baseline/Lake.jpg                        | 75       |    10.221 ms |    0.5759 ms |   0.0316 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/baseline/Lake.jpg                        | 90       |     4.284 ms |    0.0727 ms |   0.0040 ms |    0.41 |          - |          - |          - |     23112 B |         72.22 |
    | SkiaSharp   | Jpg/baseline/Lake.jpg                        | 90       |    10.573 ms |    0.9634 ms |   0.0528 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/baseline/Lake.jpg                        | 100      |     6.037 ms |    0.9861 ms |   0.0541 ms |    0.41 |          - |          - |          - |     23112 B |         72.22 |
    | SkiaSharp   | Jpg/baseline/Lake.jpg                        | 100      |    14.660 ms |    1.3667 ms |   0.0749 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/issue_2723.jpg              | 75       |     2.773 ms |    0.0111 ms |   0.0006 ms |    0.40 |          - |          - |          - |     18488 B |         57.77 |
    | SkiaSharp   | Jpg/icc-profiles/issue_2723.jpg              | 75       |     7.013 ms |    0.0465 ms |   0.0025 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/issue_2723.jpg              | 90       |     3.597 ms |    0.0169 ms |   0.0009 ms |    0.50 |          - |          - |          - |     18488 B |         57.77 |
    | SkiaSharp   | Jpg/icc-profiles/issue_2723.jpg              | 90       |     7.200 ms |    0.3080 ms |   0.0169 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/issue_2723.jpg              | 100      |     3.831 ms |    0.3590 ms |   0.0197 ms |    0.43 |          - |          - |          - |     18488 B |         57.77 |
    | SkiaSharp   | Jpg/icc-profiles/issue_2723.jpg              | 100      |     8.854 ms |    0.5174 ms |   0.0284 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/issue-129.jpg               | 75       |    20.670 ms |    2.4953 ms |   0.1368 ms |    0.42 |          - |          - |          - |     15296 B |         47.80 |
    | SkiaSharp   | Jpg/icc-profiles/issue-129.jpg               | 75       |    48.660 ms |    0.1285 ms |   0.0070 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/issue-129.jpg               | 90       |    23.010 ms |    0.7690 ms |   0.0422 ms |    0.44 |          - |          - |          - |     15296 B |         47.80 |
    | SkiaSharp   | Jpg/icc-profiles/issue-129.jpg               | 90       |    52.392 ms |    5.9125 ms |   0.3241 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/issue-129.jpg               | 100      |    27.519 ms |    3.6857 ms |   0.2020 ms |    0.49 |          - |          - |          - |     15296 B |         47.80 |
    | SkiaSharp   | Jpg/icc-profiles/issue-129.jpg               | 100      |    56.057 ms |    1.2533 ms |   0.0687 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     | 75       |     1.032 ms |    0.0837 ms |   0.0046 ms |    0.34 |          - |          - |          - |     15296 B |         47.80 |
    | SkiaSharp   | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     | 75       |     2.994 ms |    0.2531 ms |   0.0139 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     | 90       |     1.078 ms |    0.0425 ms |   0.0023 ms |    0.36 |          - |          - |          - |     15296 B |         47.80 |
    | SkiaSharp   | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     | 90       |     3.003 ms |    0.2198 ms |   0.0120 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     | 100      |     1.790 ms |    0.1420 ms |   0.0078 ms |    0.38 |          - |          - |          - |     15296 B |         47.80 |
    | SkiaSharp   | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     | 100      |     4.763 ms |    0.8900 ms |   0.0488 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  | 75       |    36.736 ms |    1.7982 ms |   0.0986 ms |    0.36 |    71.4286 |    71.4286 |    71.4286 |     22665 B |         70.83 |
    | SkiaSharp   | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  | 75       |   101.242 ms |    2.8892 ms |   0.1584 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  | 90       |    47.477 ms |    6.9763 ms |   0.3824 ms |    0.43 |    90.9091 |    90.9091 |    90.9091 |     22807 B |         71.27 |
    | SkiaSharp   | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  | 90       |   111.142 ms |    2.8738 ms |   0.1575 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |
    |             |                                              |          |              |              |             |         |            |            |            |             |               |
    | ImageSharp  | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  | 100      |    67.324 ms |   24.1032 ms |   1.3212 ms |    0.44 |   125.0000 |   125.0000 |   125.0000 |     23056 B |         72.05 |
    | SkiaSharp   | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  | 100      |   152.897 ms |    4.9118 ms |   0.2692 ms |    1.00 |          - |          - |          - |       320 B |          1.00 |

    Allocated reports managed memory only.
    */
}
