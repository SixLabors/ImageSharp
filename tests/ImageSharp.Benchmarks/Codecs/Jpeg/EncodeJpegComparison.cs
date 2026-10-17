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
        DecoderOptions options = new() { ColorProfileHandling = ColorProfileHandling.Convert };
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
}

/*
Measured 2026-10-17

| Method      | TestImage                                    | Quality  | Mean         | Error       | StdDev      | Ratio   | RatioSD   | Gen0       | Gen1       | Gen2       | Allocated   | Alloc Ratio   |
| ----------- | -------------------------------------------- | -------- | -----------: | ----------: | ----------: | ------: | --------: | ---------: | ---------: | ---------: | ----------: | ------------: |
| ImageSharp  | Jpg/baseline/Calliphora.jpg                  | 90       |     4.692 ms |   0.1011 ms |   0.0669 ms |    0.39 |      0.02 |          - |          - |          - |     16433 B |          6.94 |
| SkiaSharp   | Jpg/baseline/Calliphora.jpg                  | 90       |    12.009 ms |   1.1548 ms |   0.7639 ms |    1.00 |      0.08 |          - |          - |          - |      2368 B |          1.00 |
|             |                                              |          |              |             |             |         |           |            |            |            |             |               |
| ImageSharp  | Jpg/baseline/jpeg420exif.jpg                 | 90       |    15.009 ms |   0.1570 ms |   0.0821 ms |    0.39 |      0.00 |          - |          - |          - |     86050 B |          8.89 |
| SkiaSharp   | Jpg/baseline/jpeg420exif.jpg                 | 90       |    38.148 ms |   0.6386 ms |   0.4224 ms |    1.00 |      0.01 |          - |          - |          - |      9684 B |          1.00 |
|             |                                              |          |              |             |             |         |           |            |            |            |             |               |
| ImageSharp  | Jpg/baseline/Lake.jpg                        | 90       |     4.527 ms |   0.0392 ms |   0.0205 ms |    0.41 |      0.00 |          - |          - |          - |     24889 B |         10.51 |
| SkiaSharp   | Jpg/baseline/Lake.jpg                        | 90       |    10.949 ms |   0.1813 ms |   0.1199 ms |    1.00 |      0.01 |          - |          - |          - |      2368 B |          1.00 |
|             |                                              |          |              |             |             |         |           |            |            |            |             |               |
| ImageSharp  | Jpg/icc-profiles/issue_2723.jpg              | 90       |     3.047 ms |   0.0272 ms |   0.0180 ms |    0.40 |      0.00 |          - |          - |          - |     19432 B |         14.46 |
| SkiaSharp   | Jpg/icc-profiles/issue_2723.jpg              | 90       |     7.571 ms |   0.1675 ms |   0.0876 ms |    1.00 |      0.02 |          - |          - |          - |      1344 B |          1.00 |
|             |                                              |          |              |             |             |         |           |            |            |            |             |               |
| ImageSharp  | Jpg/icc-profiles/issue-129.jpg               | 90       |    23.860 ms |   0.1815 ms |   0.1200 ms |    0.40 |      0.03 |          - |          - |          - |     28594 B |          2.13 |
| SkiaSharp   | Jpg/icc-profiles/issue-129.jpg               | 90       |    59.741 ms |   8.3464 ms |   5.5206 ms |    1.01 |      0.12 |          - |          - |          - |     13430 B |          1.00 |
|             |                                              |          |              |             |             |         |           |            |            |            |             |               |
| ImageSharp  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     | 90       |     1.116 ms |   0.0125 ms |   0.0074 ms |    0.36 |      0.00 |          - |          - |          - |     17984 B |         21.62 |
| SkiaSharp   | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg     | 90       |     3.109 ms |   0.0667 ms |   0.0349 ms |    1.00 |      0.01 |          - |          - |          - |       832 B |          1.00 |
|             |                                              |          |              |             |             |         |           |            |            |            |             |               |
| ImageSharp  | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  | 90       |    49.409 ms |   0.7139 ms |   0.4722 ms |    0.43 |      0.01 |   100.0000 |   100.0000 |   100.0000 |     55886 B |          2.11 |
| SkiaSharp   | Jpg/issues/Issue518-Bad-RST-Progressive.jpg  | 90       |   115.903 ms |   3.1289 ms |   1.8620 ms |    1.00 |      0.02 |          - |          - |          - |     26539 B |          1.00 |
*/
