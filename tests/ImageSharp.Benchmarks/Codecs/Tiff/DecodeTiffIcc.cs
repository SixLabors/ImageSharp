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

    // Both libraries read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream inputStream;

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
        this.inputStream = new MemoryStream(this.data, false);
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
    /// Decodes the original TIFF and converts its embedded profile to sRGB through ImageMagick.
    /// </summary>
    /// <returns>The decoded image width.</returns>
    [Benchmark(Baseline = true)]
    public uint ImageMagick()
    {
        this.inputStream.Position = 0;
        using MagickImage image = new(this.inputStream, new MagickReadSettings { Format = MagickFormat.Tiff });
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
        this.inputStream.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(
            new DecoderOptions(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Preserve },
            this.inputStream);
        return image.Size;
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method      | TestImage                                   | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0      | Gen1      | Gen2     | Allocated  | Alloc Ratio |
    |------------ |-------------------------------------------- |-------------:|-------------:|-----------:|------:|--------:|----------:|----------:|---------:|-----------:|------------:|
    | ImageMagick | Tiff/icc-profiles/Perceptual_CIELAB.tiff    |  1,235.19 us |   170.571 us |   9.350 us |  1.00 |    0.01 |         - |         - |        - |    6.32 KB |        1.00 |
    | ImageSharp  | Tiff/icc-profiles/Perceptual_CIELAB.tiff    |    157.48 us |    39.497 us |   2.165 us |  0.13 |    0.00 |    1.7090 |         - |        - |   29.55 KB |        4.67 |
    |             |                                             |              |              |            |       |         |           |           |          |            |             |
    | ImageMagick | Tiff/icc-profiles/Perceptual_CMYK.tiff      | 57,363.65 us | 2,535.386 us | 138.973 us | 1.000 |    0.00 |         - |         - |        - |    7.73 KB |        1.00 |
    | ImageSharp  | Tiff/icc-profiles/Perceptual_CMYK.tiff      |    446.96 us |   260.416 us |  14.274 us | 0.008 |    0.00 | 1018.5547 | 1001.9531 | 995.1172 | 5969.18 KB |      772.46 |
    |             |                                             |              |              |            |       |         |           |           |          |            |             |
    | ImageMagick | Tiff/icc-profiles/Perceptual_RGB16.tiff     | 17,495.68 us | 6,126.566 us | 335.818 us | 1.000 |    0.02 |         - |         - |        - |     6.7 KB |        1.00 |
    | ImageSharp  | Tiff/icc-profiles/Perceptual_RGB16.tiff     |     54.46 us |     3.044 us |   0.167 us | 0.003 |    0.00 |    1.8311 |    0.1221 |        - |   30.75 KB |        4.59 |
    |             |                                             |              |              |            |       |         |           |           |          |            |             |
    | ImageMagick | Tiff/icc-profiles/Perceptual_RGB8_Webp.tiff | 17,061.02 us | 4,884.549 us | 267.739 us | 1.000 |    0.02 |         - |         - |        - |    5.88 KB |        1.00 |
    | ImageSharp  | Tiff/icc-profiles/Perceptual_RGB8_Webp.tiff |     73.19 us |     2.252 us |   0.123 us | 0.004 |    0.00 |    6.5918 |    1.0986 |        - |  108.77 KB |       18.50 |
    |             |                                             |              |              |            |       |         |           |           |          |            |             |
    | ImageMagick | Tiff/icc-profiles/Perceptual_RGB8.tiff      | 17,393.21 us | 2,057.046 us | 112.754 us | 1.000 |    0.01 |         - |         - |        - |     6.7 KB |        1.00 |
    | ImageSharp  | Tiff/icc-profiles/Perceptual_RGB8.tiff      |     56.44 us |     0.346 us |   0.019 us | 0.003 |    0.00 |    1.8311 |    0.0610 |        - |   30.52 KB |        4.56 |

    Allocated reports managed memory only.
    Both methods include conversion from the embedded ICC profile to sRGB. ImageMagick retains Q16 output; ImageSharp writes Rgba32.
    */
}
