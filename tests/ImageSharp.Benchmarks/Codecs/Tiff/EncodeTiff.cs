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
