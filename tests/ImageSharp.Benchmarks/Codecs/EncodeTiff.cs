// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing.Imaging;
using System.IO;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [MarkdownExporter]
    [HtmlExporter]
    [Config(typeof(Config.ShortMultiFramework))]
    public class EncodeTiff
    {
        private System.Drawing.Image drawing;
        private Image<Rgba32> core;

        private Configuration configuration;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(TestImages.Tiff.Calliphora_RgbUncompressed)]
        public string TestImage { get; set; }

        [Params(
            TiffCompression.None,
            TiffCompression.Deflate,
            TiffCompression.Lzw,
            TiffCompression.PackBits,
            TiffCompression.CcittGroup3Fax,
            TiffCompression.Ccitt1D)]
        public TiffCompression Compression { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.core == null)
            {
                this.configuration = new Configuration();
                this.core = Image.Load<Rgba32>(this.configuration, this.TestImageFullPath);
                this.drawing = System.Drawing.Image.FromFile(this.TestImageFullPath);
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
            using var parameters = new EncoderParameters(1)
            {
                Param = {[0] = new EncoderParameter(Encoder.Compression, (long)Cast(this.Compression))}
            };

            using var memoryStream = new MemoryStream();
            this.drawing.Save(memoryStream, codec, parameters);
        }

        [Benchmark(Description = "ImageSharp Tiff")]
        public void TiffCore()
        {
            TiffPhotometricInterpretation photometricInterpretation = TiffPhotometricInterpretation.Rgb;

            // Workaround for 1-bit bug
            if (this.Compression == TiffCompression.CcittGroup3Fax || this.Compression == TiffCompression.Ccitt1D)
            {
                photometricInterpretation = TiffPhotometricInterpretation.WhiteIsZero;
            }

            var encoder = new TiffEncoder() { Compression = this.Compression, PhotometricInterpretation = photometricInterpretation };
            using var memoryStream = new MemoryStream();
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
        {
            switch (compression)
            {
                case TiffCompression.None:
                    return EncoderValue.CompressionNone;

                case TiffCompression.CcittGroup3Fax:
                    return EncoderValue.CompressionCCITT3;

                case TiffCompression.Ccitt1D:
                    return EncoderValue.CompressionRle;

                case TiffCompression.Lzw:
                    return EncoderValue.CompressionLZW;

                default:
                    throw new System.NotSupportedException(compression.ToString());
            }
        }
    }
}
