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

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                 | TestImage                              | Compression     |          Mean |        Error |      StdDev |   Ratio |   RatioSD |       Gen0 |       Gen1 |       Gen2 |   Allocated |   Alloc Ratio |
    | ---------------------- | -------------------------------------- | --------------- | ------------: | -----------: | ----------: | ------: | --------: | ---------: | ---------: | ---------: | ----------: | ------------: |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_uncompressed.tiff  | None            |     177.35 μs |    36.869 μs |    2.021 μs |    1.00 |      0.01 |   166.5039 |   166.5039 |   166.5039 |   527.72 KB |          1.00 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_uncompressed.tiff  | None            |      90.83 μs |     4.096 μs |    0.225 μs |    0.51 |      0.01 |   111.0840 |   111.0840 |   111.0840 |   493.18 KB |          0.93 |
    |                        |                                        |                 |               |              |             |         |           |            |            |            |             |               |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_uncompressed.tiff  | Ccitt1D         |     414.84 μs |   186.439 μs |   10.219 μs |    1.00 |      0.03 |   166.5039 |   166.5039 |   166.5039 |   527.48 KB |          1.00 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_uncompressed.tiff  | Ccitt1D         |   2,418.60 μs |   220.023 μs |   12.060 μs |    5.83 |      0.13 |    89.8438 |    89.8438 |    89.8438 |   383.29 KB |          0.73 |
    |                        |                                        |                 |               |              |             |         |           |            |            |            |             |               |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_uncompressed.tiff  | CcittGroup3Fax  |   1,082.74 μs |    52.868 μs |    2.898 μs |    1.00 |      0.00 |     1.9531 |          - |          - |     59.6 KB |          1.00 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_uncompressed.tiff  | CcittGroup3Fax  |   2,414.88 μs |   109.510 μs |    6.003 μs |    2.23 |      0.01 |    89.8438 |    89.8438 |    89.8438 |   383.22 KB |          6.43 |
    |                        |                                        |                 |               |              |             |         |           |            |            |            |             |               |
    | 'System.Drawing Tiff'  | Tiff/Calliphora_rgb_uncompressed.tiff  | Lzw             |   1,311.54 μs |   297.942 μs |   16.331 μs |    1.00 |      0.02 |   166.0156 |   166.0156 |   166.0156 |   527.72 KB |          1.00 |
    | 'ImageSharp Tiff'      | Tiff/Calliphora_rgb_uncompressed.tiff  | Lzw             |   2,533.94 μs |    28.574 μs |    1.566 μs |    1.93 |      0.02 |   121.0938 |   121.0938 |   121.0938 |   524.21 KB |          0.99 |

    Allocated reports managed memory only.
    */
}
