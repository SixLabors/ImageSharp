// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Tiff;

/// <summary>
/// Measures TIFF encoding with and without an ICC return transform on the same decoded sRGB pixels.
/// </summary>
[Config(typeof(Config.Short))]
public class EncodeTiffIcc
{
    private Image<RgbaVector> image;
    private TiffEncoder encoder;
    private MemoryStream destination;

    /// <summary>
    /// Identifies the sample packing or nested JPEG path measured by the benchmark.
    /// </summary>
    public enum TiffOutput
    {
        /// <summary>
        /// Uncompressed RGB with eight bits per channel.
        /// </summary>
        Rgb8,

        /// <summary>
        /// Uncompressed RGB with sixteen bits per channel.
        /// </summary>
        Rgb16,

        /// <summary>
        /// Uncompressed RGB with single-precision floating-point channels.
        /// </summary>
        RgbFloat32,

        /// <summary>
        /// JPEG-compressed RGB with eight bits per channel.
        /// </summary>
        JpegRgb,

        /// <summary>
        /// JPEG-compressed CMYK with eight bits per channel.
        /// </summary>
        JpegCmyk
    }

    /// <summary>
    /// Gets or sets the TIFF output layout.
    /// </summary>
    [Params(TiffOutput.Rgb8, TiffOutput.Rgb16, TiffOutput.RgbFloat32, TiffOutput.JpegRgb, TiffOutput.JpegCmyk)]
    public TiffOutput Output { get; set; }

    /// <summary>
    /// Converts the real fixture to sRGB and removes its profile before measuring ordinary encoding.
    /// </summary>
    [GlobalSetup(Target = nameof(WithoutIcc))]
    public void SetupWithoutIcc() => this.Setup(ColorProfileHandling.Convert);

    /// <summary>
    /// Converts the same fixture to sRGB and retains its profile for the measured return transform.
    /// </summary>
    [GlobalSetup(Target = nameof(WithIcc))]
    public void SetupWithIcc() => this.Setup(ColorProfileHandling.Preserve);

    /// <summary>
    /// Prepares the real source, explicit output settings and reusable stream outside measurement.
    /// </summary>
    /// <param name="handling">Whether to retain the original profile after decoding to sRGB.</param>
    private void Setup(ColorProfileHandling handling)
    {
        // Use each fixture's own profile. Float pixels retain the RGB16 fixture's precision before final sample packing.
        string testImage = this.Output == TiffOutput.JpegCmyk ? TestImages.Tiff.Icc.PerceptualCmyk : TestImages.Tiff.Icc.PerceptualRgb16;
        DecoderOptions options = new(Configuration.Default) { ColorProfileHandling = handling };
        this.image = Image.Load<RgbaVector>(options, Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, testImage));

        // Keep output settings identical within each WithIcc/WithoutIcc pair. JPEG owns its return transform inside the nested encoder.
        this.encoder = new TiffEncoder
        {
            BitsPerPixel = this.Output switch
            {
                TiffOutput.Rgb16 => TiffBitsPerPixel.Bit48,
                TiffOutput.RgbFloat32 => TiffBitsPerPixel.Bit96,
                TiffOutput.JpegCmyk => TiffBitsPerPixel.Bit32,
                _ => TiffBitsPerPixel.Bit24
            },
            SampleFormat = this.Output == TiffOutput.RgbFloat32 ? TiffSampleFormat.Float : TiffSampleFormat.UnsignedInteger,
            PhotometricInterpretation = this.Output == TiffOutput.JpegCmyk ? TiffPhotometricInterpretation.Separated : TiffPhotometricInterpretation.Rgb,
            Compression = this.Output is TiffOutput.JpegRgb or TiffOutput.JpegCmyk ? TiffCompression.Jpeg : TiffCompression.None
        };

        this.destination = new MemoryStream();

        // Warm stream capacity and check that compatibility selection did not silently omit the measured profile.
        this.image.Save(this.destination, this.encoder);
        this.destination.Position = 0;
        ImageInfo encoded = Image.Identify(this.destination);
        if ((encoded.FrameMetadataCollection[0].IccProfile is not null) != (handling == ColorProfileHandling.Preserve))
        {
            throw new InvalidOperationException("The TIFF benchmark did not encode the selected ICC profile state.");
        }
    }

    /// <summary>
    /// Releases the source image and reusable output stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.image.Dispose();
        this.destination.Dispose();
    }

    /// <summary>
    /// Encodes the sRGB pixels without an ICC return transform or embedded profile.
    /// </summary>
    /// <returns>The encoded TIFF length in bytes.</returns>
    [Benchmark(Baseline = true)]
    public long WithoutIcc() => this.Encode();

    /// <summary>
    /// Converts the same sRGB pixels back to the retained profile and embeds it in the TIFF.
    /// </summary>
    /// <returns>The encoded TIFF length in bytes.</returns>
    [Benchmark]
    public long WithIcc() => this.Encode();

    /// <summary>
    /// Encodes one image while reusing the output stream's capacity between invocations.
    /// </summary>
    /// <returns>The encoded TIFF length in bytes.</returns>
    private long Encode()
    {
        // Truncation removes any bytes left by a longer previous encoding and resets the stream position.
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

    | Method      | Output      |        Mean |        Error |     StdDev |   Ratio |   RatioSD |      Gen0 |      Gen1 |      Gen2 |   Allocated |   Alloc Ratio |
    | ----------- | ----------- | ----------: | -----------: | ---------: | ------: | --------: | --------: | --------: | --------: | ----------: | ------------: |
    | WithoutIcc  | Rgb8        |    23.21 μs |     3.182 μs |   0.174 μs |    1.00 |      0.01 |    0.9766 |         - |         - |    15.95 KB |          1.00 |
    | WithIcc     | Rgb8        |    71.99 μs |     2.156 μs |   0.118 μs |    3.10 |      0.02 |    1.3428 |         - |         - |    22.05 KB |          1.38 |
    |             |             |             |              |            |         |           |           |           |           |             |               |
    | WithoutIcc  | Rgb16       |    31.75 μs |     1.123 μs |   0.062 μs |    1.00 |      0.00 |    0.9766 |         - |         - |    16.21 KB |          1.00 |
    | WithIcc     | Rgb16       |    80.52 μs |     2.432 μs |   0.133 μs |    2.54 |      0.01 |    1.3428 |         - |         - |     22.3 KB |          1.38 |
    |             |             |             |              |            |         |           |           |           |           |             |               |
    | WithoutIcc  | RgbFloat32  |    11.76 μs |     2.994 μs |   0.164 μs |    1.00 |      0.02 |    1.0529 |    0.0153 |         - |     17.2 KB |          1.00 |
    | WithIcc     | RgbFloat32  |    61.19 μs |    10.159 μs |   0.557 μs |    5.20 |      0.08 |    1.3428 |         - |         - |    23.13 KB |          1.34 |
    |             |             |             |              |            |         |           |           |           |           |             |               |
    | WithoutIcc  | JpegRgb     |    55.91 μs |     3.140 μs |   0.172 μs |    1.00 |      0.00 |    1.5869 |         - |         - |    26.08 KB |          1.00 |
    | WithIcc     | JpegRgb     |   100.66 μs |    17.582 μs |   0.964 μs |    1.80 |      0.02 |    1.9531 |         - |         - |    32.16 KB |          1.23 |
    |             |             |             |              |            |         |           |           |           |           |             |               |
    | WithoutIcc  | JpegCmyk    |    68.93 μs |     3.683 μs |   0.202 μs |    1.00 |      0.00 |    1.7090 |         - |         - |     28.5 KB |          1.00 |
    | WithIcc     | JpegCmyk    |   335.16 μs |   125.576 μs |   6.883 μs |    4.86 |      0.09 |   93.7500 |   91.7969 |   91.7969 |   578.83 KB |         20.31 |

    Allocated reports managed memory only.
    WithIcc includes the return transform and profile output. WithoutIcc encodes the same sRGB pixels without that profile.
    */
}
