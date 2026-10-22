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
}

/* Measured 2026-10-18

| Method     | Output     |      Mean |      Error |   StdDev | Ratio | RatioSD |    Gen0 |    Gen1 |    Gen2 | Allocated | Alloc Ratio |
| ---------- | ---------- | --------: | ---------: | -------: | ----: | ------: | ------: | ------: | ------: | --------: | ----------: |
| WithoutIcc | Rgb8       |  20.82 μs |   2.670 μs | 0.146 μs |  1.00 |    0.01 |  0.9766 |       - |       - |  15.98 KB |        1.00 |
| WithIcc    | Rgb8       |  71.32 μs |  27.327 μs | 1.498 μs |  3.43 |    0.07 |  1.3428 |       - |       - |  22.18 KB |        1.39 |
|            |            |           |            |          |       |         |         |         |         |           |             |
| WithoutIcc | Rgb16      |  29.29 μs |   5.028 μs | 0.276 μs |  1.00 |    0.01 |  0.9766 |       - |       - |  16.23 KB |        1.00 |
| WithIcc    | Rgb16      | 101.83 μs |  90.737 μs | 4.974 μs |  3.48 |    0.15 |  1.3428 |       - |       - |  22.44 KB |        1.38 |
|            |            |           |            |          |       |         |         |         |         |           |             |
| WithoutIcc | RgbFloat32 |  14.29 μs |  16.986 μs | 0.931 μs |  1.00 |    0.08 |  1.0529 |  0.0153 |       - |   17.2 KB |        1.00 |
| WithIcc    | RgbFloat32 |  76.13 μs | 104.951 μs | 5.753 μs |  5.34 |    0.46 |  1.4038 |       - |       - |  23.27 KB |        1.35 |
|            |            |           |            |          |       |         |         |         |         |           |             |
| WithoutIcc | JpegRgb    |  66.87 μs | 127.165 μs | 6.970 μs |  1.01 |    0.13 |  1.5869 |       - |       - |  25.98 KB |        1.00 |
| WithIcc    | JpegRgb    |  99.78 μs | 118.329 μs | 6.486 μs |  1.50 |    0.15 |  1.9531 |       - |       - |  32.21 KB |        1.24 |
|            |            |           |            |          |       |         |         |         |         |           |             |
| WithoutIcc | JpegCmyk   |  71.74 μs |   4.333 μs | 0.237 μs |  1.00 |    0.00 |  1.7090 |       - |       - |  28.38 KB |        1.00 |
| WithIcc    | JpegCmyk   | 328.49 μs |  47.518 μs | 2.605 μs |  4.58 |    0.03 | 95.7031 | 93.7500 | 93.7500 | 578.83 KB |       20.40 |
*/
