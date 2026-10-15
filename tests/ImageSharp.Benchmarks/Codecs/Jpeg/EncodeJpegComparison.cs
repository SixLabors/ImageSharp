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
Measured 2026-10-15

| Method      | TestImage                                   | Quality  | Mean         | Error       | StdDev      | Ratio   | RatioSD   | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio   |
|-------------|---------------------------------------------|----------|-------------:|------------:|------------:|--------:|----------:|----------:|----------:|----------:|------------:|--------------:|
| ImageSharp  | Jpg/baseline/Calliphora.jpg                 | 90       |     4.970 ms |   0.5676 ms |   0.3754 ms |    0.43 |      0.03 |         - |         - |         - |     16433 B |          6.94 |
| SkiaSharp   | Jpg/baseline/Calliphora.jpg                 | 90       |    11.669 ms |   0.5958 ms |   0.3941 ms |    1.00 |      0.05 |         - |         - |         - |      2368 B |          1.00 |
|             |                                             |          |              |             |             |         |           |           |           |           |             |               |
| ImageSharp  | Jpg/baseline/jpeg420exif.jpg                | 90       |    15.225 ms |   0.6729 ms |   0.4004 ms |    0.37 |      0.02 |         - |         - |         - |     86050 B |          8.27 |
| SkiaSharp   | Jpg/baseline/jpeg420exif.jpg                | 90       |    40.858 ms |   2.7875 ms |   1.6588 ms |    1.00 |      0.05 |         - |         - |         - |     10404 B |          1.00 |
|             |                                             |          |              |             |             |         |           |           |           |           |             |               |
| ImageSharp  | Jpg/baseline/Lake.jpg                       | 90       |     4.511 ms |   0.0989 ms |   0.0654 ms |    0.41 |      0.01 |         - |         - |         - |     24889 B |         10.51 |
| SkiaSharp   | Jpg/baseline/Lake.jpg                       | 90       |    10.986 ms |   0.3453 ms |   0.2055 ms |    1.00 |      0.02 |         - |         - |         - |      2368 B |          1.00 |
|             |                                             |          |              |             |             |         |           |           |           |           |             |               |
| ImageSharp  | Jpg/icc-profiles/issue_2723.jpg             | 90       |     3.217 ms |   0.0493 ms |   0.0326 ms |    0.44 |      0.01 |         - |         - |         - |     19432 B |         14.46 |
| SkiaSharp   | Jpg/icc-profiles/issue_2723.jpg             | 90       |     7.297 ms |   0.1767 ms |   0.1052 ms |    1.00 |      0.02 |         - |         - |         - |      1344 B |          1.00 |
|             |                                             |          |              |             |             |         |           |           |           |           |             |               |
| ImageSharp  | Jpg/icc-profiles/issue-129.jpg              | 90       |    26.849 ms |   1.9583 ms |   1.0242 ms |    0.45 |      0.02 |         - |         - |         - |     28594 B |          1.92 |
| SkiaSharp   | Jpg/icc-profiles/issue-129.jpg              | 90       |    59.493 ms |   4.0145 ms |   2.3890 ms |    1.00 |      0.05 |         - |         - |         - |     14886 B |          1.00 |
|             |                                             |          |              |             |             |         |           |           |           |           |             |               |
| ImageSharp  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg    | 90       |     1.165 ms |   0.0510 ms |   0.0337 ms |    0.38 |      0.01 |         - |         - |         - |     17984 B |         21.62 |
| SkiaSharp   | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg    | 90       |     3.048 ms |   0.0685 ms |   0.0408 ms |    1.00 |      0.02 |         - |         - |         - |       832 B |          1.00 |
|             |                                             |          |              |             |             |         |           |           |           |           |             |               |
| ImageSharp  | Jpg/issues/Issue518-Bad-RST-Progressive.jpg | 90       |    47.578 ms |   0.5485 ms |   0.2869 ms |    0.42 |      0.00 |   90.9091 |   90.9091 |   90.9091 |     52841 B |          1.99 |
| SkiaSharp   | Jpg/issues/Issue518-Bad-RST-Progressive.jpg | 90       |   112.345 ms |   1.1040 ms |   0.5774 ms |    1.00 |      0.01 |         - |         - |         - |     26539 B |          1.00 |
*/
