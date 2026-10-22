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
}

/* Measured 2026-10-18

| Method      | TestImage                                   |         Mean |        Error |     StdDev | Ratio | RatioSD |      Gen0 |     Gen1 |     Gen2 |  Allocated | Alloc Ratio |
| ----------- | ------------------------------------------- | -----------: | -----------: | ---------: | ----: | ------: | --------: | -------: | -------: | ---------: | ----------: |
| ImageMagick | Tiff/icc-profiles/Perceptual_CIELAB.tiff    |  1,250.50 μs |    601.22 μs |  32.955 μs |  1.00 |    0.03 |         - |        - |        - |    6.38 KB |        1.00 |
| ImageSharp  | Tiff/icc-profiles/Perceptual_CIELAB.tiff    |    224.50 μs |     19.76 μs |   1.083 μs |  0.18 |    0.00 |    1.7090 |        - |        - |   30.48 KB |        4.77 |
|             |                                             |              |              |            |       |         |           |          |          |            |             |
| ImageMagick | Tiff/icc-profiles/Perceptual_CMYK.tiff      | 59,862.83 μs | 11,566.34 μs | 633.990 μs | 1.000 |    0.01 |         - |        - |        - |    7.76 KB |        1.00 |
| ImageSharp  | Tiff/icc-profiles/Perceptual_CMYK.tiff      |    533.78 μs |    224.10 μs |  12.284 μs | 0.009 |    0.00 | 1001.9531 | 989.2578 | 977.5391 | 5955.08 KB |      767.14 |
|             |                                             |              |              |            |       |         |           |          |          |            |             |
| ImageMagick | Tiff/icc-profiles/Perceptual_RGB16.tiff     | 18,240.86 μs |  4,670.44 μs | 256.002 μs | 1.000 |    0.02 |         - |        - |        - |    6.75 KB |        1.00 |
| ImageSharp  | Tiff/icc-profiles/Perceptual_RGB16.tiff     |     81.45 μs |     51.50 μs |   2.823 μs | 0.004 |    0.00 |    2.1973 |        - |        - |   37.67 KB |        5.58 |
|             |                                             |              |              |            |       |         |           |          |          |            |             |
| ImageMagick | Tiff/icc-profiles/Perceptual_RGB8_Webp.tiff | 17,053.08 μs |  1,698.98 μs |  93.127 μs | 1.000 |    0.01 |         - |        - |        - |    5.93 KB |        1.00 |
| ImageSharp  | Tiff/icc-profiles/Perceptual_RGB8_Webp.tiff |     84.32 μs |     35.61 μs |   1.952 μs | 0.005 |    0.00 |    6.7139 |   1.0986 |        - |  110.68 KB |       18.65 |
|             |                                             |              |              |            |       |         |           |          |          |            |             |
| ImageMagick | Tiff/icc-profiles/Perceptual_RGB8.tiff      | 18,451.70 μs | 16,321.20 μs | 894.620 μs | 1.002 |    0.06 |         - |        - |        - |    6.75 KB |        1.00 |
| ImageSharp  | Tiff/icc-profiles/Perceptual_RGB8.tiff      |     74.70 μs |     36.08 μs |   1.978 μs | 0.004 |    0.00 |    2.1973 |        - |        - |   37.66 KB |        5.58 |
*/
