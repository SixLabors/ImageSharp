// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class EncodeTiff
{
    private FileStream stream;
    private SDImage drawing;
    private Image<Rgba32> core;

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Tiff.Calliphora_RgbUncompressed)]
    public string TestImage { get; set; }

    [Params(
        TiffCompression.None,

        // System.Drawing does not support Deflate or PackBits
        // TiffCompression.Deflate,
        // TiffCompression.PackBits,
        TiffCompression.Lzw,
        TiffCompression.CcittGroup3Fax,
        TiffCompression.Ccitt1D)]
    public TiffCompression Compression { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.stream == null)
        {
            this.stream = File.OpenRead(this.TestImageFullPath);
            this.core = Image.Load<Rgba32>(this.stream);
            this.stream.Position = 0;
            this.drawing = SDImage.FromStream(this.stream);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.core.Dispose();
        this.drawing.Dispose();
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Tiff")]
    public void SystemDrawing()
    {
        ImageCodecInfo codec = FindCodecForType("image/tiff");
        using EncoderParameters parameters = new(1)
        {
            Param = { [0] = new EncoderParameter(Encoder.Compression, (long)Cast(this.Compression)) }
        };

        using MemoryStream memoryStream = new();
        this.drawing.Save(memoryStream, codec, parameters);
    }

    [Benchmark(Description = "ImageSharp Tiff")]
    public void TiffCore()
    {
        TiffPhotometricInterpretation photometricInterpretation =
            IsOneBitCompression(this.Compression) ?
                TiffPhotometricInterpretation.WhiteIsZero :
                TiffPhotometricInterpretation.Rgb;

        TiffEncoder encoder = new() { Compression = this.Compression, PhotometricInterpretation = photometricInterpretation };
        using MemoryStream memoryStream = new();
        this.core.SaveAsTiff(memoryStream, encoder);
    }

    private static ImageCodecInfo FindCodecForType(string mimeType)
    {
        ImageCodecInfo[] imgEncoders = ImageCodecInfo.GetImageEncoders();

        for (int i = 0; i < imgEncoders.GetLength(0); i++)
        {
            if (imgEncoders[i].MimeType == mimeType)
            {
                return imgEncoders[i];
            }
        }

        return null;
    }

    private static EncoderValue Cast(TiffCompression compression)
        => compression switch
        {
            TiffCompression.None => EncoderValue.CompressionNone,
            TiffCompression.CcittGroup3Fax => EncoderValue.CompressionCCITT3,
            TiffCompression.Ccitt1D => EncoderValue.CompressionRle,
            TiffCompression.Lzw => EncoderValue.CompressionLZW,
            _ => throw new NotSupportedException(compression.ToString()),
        };

    public static bool IsOneBitCompression(TiffCompression compression)
        => compression is TiffCompression.Ccitt1D or TiffCompression.CcittGroup3Fax or TiffCompression.CcittGroup4Fax;
}

/* Measured 2026-10-18

| Method                | TestImage                             | Compression    |        Mean |      Error |    StdDev | Ratio | RatioSD |     Gen0 |     Gen1 |     Gen2 | Allocated | Alloc Ratio |
| --------------------- | ------------------------------------- | -------------- | ----------: | ---------: | --------: | ----: | ------: | -------: | -------: | -------: | --------: | ----------: |
| 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff | None           |   169.07 μs |   5.050 μs |  0.277 μs |  1.00 |    0.00 | 166.5039 | 166.5039 | 166.5039 | 527.72 KB |        1.00 |
| 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff | None           |    90.77 μs |   3.496 μs |  0.192 μs |  0.54 |    0.00 | 111.0840 | 111.0840 | 111.0840 | 493.14 KB |        0.93 |
|                       |                                       |                |             |            |           |       |         |          |          |          |           |             |
| 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff | Ccitt1D        |   426.25 μs | 287.721 μs | 15.771 μs |  1.00 |    0.05 | 166.5039 | 166.5039 | 166.5039 | 527.48 KB |        1.00 |
| 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff | Ccitt1D        | 2,429.11 μs |  35.425 μs |  1.942 μs |  5.70 |    0.18 |  89.8438 |  89.8438 |  89.8438 | 383.58 KB |        0.73 |
|                       |                                       |                |             |            |           |       |         |          |          |          |           |             |
| 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff | CcittGroup3Fax | 1,083.96 μs | 108.257 μs |  5.934 μs |  1.00 |    0.01 |   1.9531 |        - |        - |   59.6 KB |        1.00 |
| 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff | CcittGroup3Fax | 2,429.07 μs | 160.447 μs |  8.795 μs |  2.24 |    0.01 |  89.8438 |  89.8438 |  89.8438 | 383.51 KB |        6.43 |
|                       |                                       |                |             |            |           |       |         |          |          |          |           |             |
| 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff | Lzw            | 1,344.39 μs | 324.661 μs | 17.796 μs |  1.00 |    0.02 | 166.0156 | 166.0156 | 166.0156 | 527.72 KB |        1.00 |
| 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff | Lzw            | 2,577.93 μs | 332.148 μs | 18.206 μs |  1.92 |    0.03 | 121.0938 | 121.0938 | 121.0938 | 524.16 KB |        0.99 |
*/
