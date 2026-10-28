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
        DecoderOptions options = new(Configuration.Default) { ColorProfileHandling = handling };
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

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method      | TestImage                                 |           Mean |        Error |      StdDev |   Ratio |   RatioSD |   Allocated |   Alloc Ratio |
    | ----------- | ----------------------------------------- | -------------: | -----------: | ----------: | ------: | --------: | ----------: | ------------: |
    | WithoutIcc  | Jpg/icc-profiles/issue_2723.jpg           |     5,866.9 μs |     329.6 μs |    18.07 μs |    1.00 |      0.00 |    15.78 KB |          1.00 |
    | WithIcc     | Jpg/icc-profiles/issue_2723.jpg           |    26,110.6 μs |   3,018.9 μs |   165.48 μs |    4.45 |      0.03 |    21.12 KB |          1.34 |
    |             |                                           |                |              |             |         |           |             |               |
    | WithoutIcc  | Jpg/icc-profiles/issue-129.jpg            |    41,249.9 μs |   4,332.1 μs |   237.45 μs |    1.00 |      0.01 |    12.66 KB |          1.00 |
    | WithIcc     | Jpg/icc-profiles/issue-129.jpg            |   176,385.1 μs |   6,610.1 μs |   362.32 μs |    4.28 |      0.02 |    18.45 KB |          1.46 |
    |             |                                           |                |              |             |         |           |             |               |
    | WithoutIcc  | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg  |       940.1 μs |     110.9 μs |     6.08 μs |    1.00 |      0.01 |    14.94 KB |          1.00 |
    | WithIcc     | Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg  |     2,324.8 μs |     123.3 μs |     6.76 μs |    2.47 |      0.02 |    20.02 KB |          1.34 |

    Allocated reports managed memory only.
    WithIcc includes the return transform and profile output. WithoutIcc encodes the same sRGB pixels without that profile.
    */
}
