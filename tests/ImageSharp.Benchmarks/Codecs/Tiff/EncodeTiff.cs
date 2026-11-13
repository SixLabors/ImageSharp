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

    // Both libraries write into this stream. Each call truncates it, so after the first call it has the full capacity,
    // and the results measure the encoders instead of the growth of a new stream.
    // Truncation is necessary because the GDI+ TIFF encoder fails when the stream keeps bytes from a previous call.
    private MemoryStream outputStream;

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
            this.outputStream = new MemoryStream();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.core.Dispose();
        this.drawing.Dispose();
        this.outputStream.Dispose();
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Tiff")]
    public void SystemDrawing()
    {
        ImageCodecInfo codec = FindCodecForType("image/tiff");
        using EncoderParameters parameters = new(1)
        {
            Param = { [0] = new EncoderParameter(Encoder.Compression, (long)Cast(this.Compression)) }
        };

        this.outputStream.SetLength(0);
        this.drawing.Save(this.outputStream, codec, parameters);
    }

    [Benchmark(Description = "ImageSharp Tiff")]
    public void TiffCore()
    {
        TiffPhotometricInterpretation photometricInterpretation =
            IsOneBitCompression(this.Compression) ?
                TiffPhotometricInterpretation.WhiteIsZero :
                TiffPhotometricInterpretation.Rgb;

        TiffEncoder encoder = new() { Compression = this.Compression, PhotometricInterpretation = photometricInterpretation };
        this.outputStream.SetLength(0);
        this.core.SaveAsTiff(this.outputStream, encoder);
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

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                | TestImage                             | Compression    | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
    |---------------------- |-------------------------------------- |--------------- |------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff | None           |    92.81 us |  79.501 us |  4.358 us |  1.00 |    0.06 | 0.1221 |   3.28 KB |        1.00 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff | None           |    11.55 us |   1.084 us |  0.059 us |  0.12 |    0.01 | 0.7477 |  12.23 KB |        3.73 |
    |                       |                                       |                |             |            |           |       |         |        |           |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff | Ccitt1D        |   351.24 us | 100.679 us |  5.519 us |  1.00 |    0.02 |      - |   3.28 KB |        1.00 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff | Ccitt1D        | 2,358.26 us | 162.648 us |  8.915 us |  6.72 |    0.09 |      - |  17.78 KB |        5.42 |
    |                       |                                       |                |             |            |           |       |         |        |           |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff | CcittGroup3Fax | 1,083.60 us | 171.201 us |  9.384 us |  1.00 |    0.01 |      - |   3.28 KB |        1.00 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff | CcittGroup3Fax | 2,360.68 us |  63.778 us |  3.496 us |  2.18 |    0.02 |      - |  17.71 KB |        5.40 |
    |                       |                                       |                |             |            |           |       |         |        |           |             |
    | 'System.Drawing Tiff' | Tiff/Calliphora_rgb_uncompressed.tiff | Lzw            | 1,216.59 us | 110.588 us |  6.062 us |  1.00 |    0.01 |      - |   3.28 KB |        1.00 |
    | 'ImageSharp Tiff'     | Tiff/Calliphora_rgb_uncompressed.tiff | Lzw            | 2,521.41 us | 408.319 us | 22.381 us |  2.07 |    0.02 |      - |  12.09 KB |        3.68 |

    Allocated reports managed memory only.
    */
}
