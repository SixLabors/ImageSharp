// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Compares TIFF decoding with embedded-profile conversion to sRGB.
/// </summary>
[Config(typeof(Config.Short))]
public class DecodeTiffIcc
{
    private byte[] data;
    private ImageMagick.ColorProfile targetProfile;
    private RenderingIntent renderingIntent;

    /// <summary>
    /// Gets or sets the real TIFF supplying encoded samples and an embedded ICC profile.
    /// </summary>
    [Params(
        TestImages.Tiff.Icc.PerceptualRgb8,
        TestImages.Tiff.Icc.PerceptualRgb16,
        TestImages.Tiff.Icc.PerceptualCmyk,
        TestImages.Tiff.Icc.PerceptualCieLab,
        TestImages.Tiff.Icc.PerceptualRgb8Webp)]
    public string TestImage { get; set; }

    /// <summary>
    /// Loads the original TIFF and prepares the native converter's target profile before measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.data = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage));
        ImageInfo info = Image.Identify(this.data);
        if (!IccProfile.TryGetIccProfileForColorConversion(info.Metadata.IccProfile, out IccProfile profile))
        {
            throw new InvalidOperationException("The TIFF benchmark requires an embedded profile that converts to sRGB.");
        }

        // Match the reference decoder's intent mapping. A MagickImage constructor alone does not request ICC conversion.
        this.renderingIntent = profile.Header.RenderingIntent switch
        {
            IccRenderingIntent.Perceptual => RenderingIntent.Perceptual,
            IccRenderingIntent.MediaRelativeColorimetric => RenderingIntent.Relative,
            IccRenderingIntent.Saturation => RenderingIntent.Saturation,
            IccRenderingIntent.AbsoluteColorimetric => RenderingIntent.Absolute,
            _ => RenderingIntent.Undefined
        };

        this.targetProfile = new ImageMagick.ColorProfile(CompactSrgbV4Profile.Profile.ToByteArray());
    }

    /// <summary>
    /// Decodes the original TIFF and converts its embedded profile to sRGB through ImageMagick.
    /// </summary>
    /// <returns>The decoded image width.</returns>
    [Benchmark(Baseline = true)]
    public uint ImageMagick()
    {
        using MemoryStream source = new(this.data);
        using MagickImage image = new(source, new MagickReadSettings { Format = MagickFormat.Tiff });
        image.RenderingIntent = this.renderingIntent;
        image.BlackPointCompensation = false;

        // Include the profile transform in the timed operation; keep native pixels at Magick.NET's Q16 precision.
        if (!image.TransformColorSpace(this.targetProfile))
        {
            throw new InvalidOperationException("ImageMagick did not apply the TIFF's embedded ICC profile.");
        }

        return image.Width;
    }

    /// <summary>
    /// Decodes the original TIFF to Rgba32 in sRGB while retaining its embedded profile.
    /// </summary>
    /// <returns>The decoded image size.</returns>
    [Benchmark]
    public Size ImageSharp()
    {
        using MemoryStream source = new(this.data);
        using Image<Rgba32> image = Image.Load<Rgba32>(
            new DecoderOptions(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Preserve },
            source);
        return image.Size;
    }

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method       | TestImage                                    |           Mean |          Error |      StdDev |   Ratio |   RatioSD |        Gen0 |       Gen1 |       Gen2 |    Allocated |   Alloc Ratio |
    | ------------ | -------------------------------------------- | -------------: | -------------: | ----------: | ------: | --------: | ----------: | ---------: | ---------: | -----------: | ------------: |
    | ImageMagick  | Tiff/icc-profiles/Perceptual_CIELAB.tiff     |    1,141.99 μs |     288.296 μs |   15.802 μs |    1.00 |      0.02 |           - |          - |          - |      6.38 KB |          1.00 |
    | ImageSharp   | Tiff/icc-profiles/Perceptual_CIELAB.tiff     |      157.66 μs |       3.944 μs |    0.216 μs |    0.14 |      0.00 |      1.7090 |          - |          - |     29.71 KB |          4.65 |
    |              |                                              |                |                |             |         |           |             |            |            |              |               |
    | ImageMagick  | Tiff/icc-profiles/Perceptual_CMYK.tiff       |   56,929.37 μs |   1,550.725 μs |   85.000 μs |   1.000 |      0.00 |           - |          - |          - |      7.76 KB |          1.00 |
    | ImageSharp   | Tiff/icc-profiles/Perceptual_CMYK.tiff       |      423.49 μs |     223.138 μs |   12.231 μs |   0.007 |      0.00 |   1000.0000 |   983.3984 |   977.5391 |   4944.71 KB |        636.98 |
    |              |                                              |                |                |             |         |           |             |            |            |              |               |
    | ImageMagick  | Tiff/icc-profiles/Perceptual_RGB16.tiff      |   16,853.11 μs |   1,142.062 μs |   62.600 μs |   1.000 |      0.00 |           - |          - |          - |      6.75 KB |          1.00 |
    | ImageSharp   | Tiff/icc-profiles/Perceptual_RGB16.tiff      |       92.18 μs |      16.730 μs |    0.917 μs |   0.005 |      0.00 |      1.9531 |          - |          - |     33.05 KB |          4.89 |
    |              |                                              |                |                |             |         |           |             |            |            |              |               |
    | ImageMagick  | Tiff/icc-profiles/Perceptual_RGB8_Webp.tiff  |   16,155.79 μs |     855.412 μs |   46.888 μs |   1.000 |      0.00 |           - |          - |          - |      5.93 KB |          1.00 |
    | ImageSharp   | Tiff/icc-profiles/Perceptual_RGB8_Webp.tiff  |       84.89 μs |       2.954 μs |    0.162 μs |   0.005 |      0.00 |      6.7139 |     1.3428 |          - |    110.75 KB |         18.66 |
    |              |                                              |                |                |             |         |           |             |            |            |              |               |
    | ImageMagick  | Tiff/icc-profiles/Perceptual_RGB8.tiff       |   17,147.35 μs |     823.088 μs |   45.116 μs |   1.000 |      0.00 |           - |          - |          - |      6.75 KB |          1.00 |
    | ImageSharp   | Tiff/icc-profiles/Perceptual_RGB8.tiff       |       69.64 μs |       3.953 μs |    0.217 μs |   0.004 |      0.00 |      1.9531 |          - |          - |     32.59 KB |          4.83 |

    Allocated reports managed memory only.
    Both methods include conversion from the embedded ICC profile to sRGB. ImageMagick retains Q16 output; ImageSharp writes Rgba32.
    */
}
