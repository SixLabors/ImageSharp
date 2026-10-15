// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Measures JPEG encoding with and without an ICC return transform on the same decoded sRGB pixels.
/// </summary>
[Config(typeof(Config.Short))]
public class EncodeJpegIcc
{
    private Image<Rgb24> image;
    private JpegEncoder encoder;
    private MemoryStream destination;

    /// <summary>
    /// Gets or sets the real JPEG supplying the pixels, original profile and encoded color type.
    /// </summary>
    [Params(TestImages.Jpeg.ICC.AdobeRgb, TestImages.Jpeg.ICC.CMYK, TestImages.Jpeg.ICC.YCCK)]
    public string TestImage { get; set; }

    /// <summary>
    /// Decodes to sRGB and removes the profile before measuring ordinary encoding.
    /// </summary>
    [GlobalSetup(Target = nameof(WithoutIcc))]
    public void SetupWithoutIcc() => this.Setup(ColorProfileHandling.Convert);

    /// <summary>
    /// Decodes to the same sRGB pixels and retains the profile for the measured return transform.
    /// </summary>
    [GlobalSetup(Target = nameof(WithIcc))]
    public void SetupWithIcc() => this.Setup(ColorProfileHandling.Preserve);

    /// <summary>
    /// Prepares the source and reusable output stream outside the measured encoding operation.
    /// </summary>
    /// <param name="handling">Whether to retain the original profile after conversion to sRGB.</param>
    private void Setup(ColorProfileHandling handling)
    {
        DecoderOptions options = new() { ColorProfileHandling = handling };
        this.image = Image.Load<Rgb24>(options, Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage));

        // Preserve and Convert decode the same pixels. Retain the source layout in both cases so the profile is the only encoder input that changes.
        this.encoder = new JpegEncoder { Quality = 90, ColorType = this.image.Metadata.GetJpegMetadata().ColorType };
        this.destination = new MemoryStream();

        // Warm the stream capacity and verify that the selected layout can retain the profile before timing it.
        this.image.Save(this.destination, this.encoder);
        this.destination.Position = 0;
        ImageInfo encoded = Image.Identify(this.destination);
        if ((encoded.Metadata.IccProfile is not null) != (handling == ColorProfileHandling.Preserve))
        {
            throw new InvalidOperationException("The JPEG benchmark did not encode the selected ICC profile state.");
        }
    }

    /// <summary>
    /// Releases the decoded source and reusable output stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.image.Dispose();
        this.destination.Dispose();
    }

    /// <summary>
    /// Encodes the sRGB pixels without an embedded profile or ICC return transform.
    /// </summary>
    /// <returns>The encoded JPEG length in bytes.</returns>
    [Benchmark(Baseline = true)]
    public long WithoutIcc() => this.Encode();

    /// <summary>
    /// Converts the same pixels back to the retained profile and embeds it in the JPEG.
    /// </summary>
    /// <returns>The encoded JPEG length in bytes.</returns>
    [Benchmark]
    public long WithIcc() => this.Encode();

    /// <summary>
    /// Encodes one image while reusing stream capacity between invocations.
    /// </summary>
    /// <returns>The encoded JPEG length in bytes.</returns>
    private long Encode()
    {
        // Truncate the previous output; Position alone can leave trailing bytes after a shorter encoding.
        this.destination.SetLength(0);
        this.image.Save(this.destination, this.encoder);
        return this.destination.Length;
    }
}

/*
Measured 2026-10-15

| Method      | TestImage                                 | Mean           | Error         | StdDev        | Ratio   | RatioSD   | Allocated    | Alloc Ratio   |
|-------------|-------------------------------------------|---------------:|--------------:|--------------:|--------:|----------:|-------------:|--------------:|
| WithoutIcc  | Jpg/icc-profiles/issue_2723.jpg           |     6,453.7 us |     230.48 us |     152.45 us |    1.00 |      0.03 |     16.61 KB |          1.00 |
| WithIcc     | Jpg/icc-profiles/issue_2723.jpg           |   116,873.6 us |     313.86 us |     164.16 us |   18.12 |      0.41 |   1053.77 KB |         63.45 |
|             |                                           |                |               |               |         |           |              |               |
| WithoutIcc  | Jpg/icc-profiles/issue-129.jpg            |    41,428.4 us |     192.33 us |     114.45 us |    1.00 |      0.00 |     29.88 KB |          1.00 |
| WithIcc     | Jpg/icc-profiles/issue-129.jpg            |   869,674.3 us |   4,654.81 us |   2,770.00 us |   20.99 |      0.08 |    3474.9 KB |        116.28 |
|             |                                           |                |               |               |         |           |              |               |
| WithoutIcc  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg  |       914.6 us |       6.07 us |       3.17 us |    1.00 |      0.00 |     16.24 KB |          1.00 |
| WithIcc     | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg  |    17,822.9 us |     104.58 us |      62.23 us |   19.49 |      0.09 |    419.11 KB |         25.80 |
*/
