// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing.Imaging;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    public class EncodeJpeg
    {
        [Params(75, 90, 100)]
        public int Quality;

        private const string TestImage = TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr;

        // System.Drawing
        private SDImage bmpDrawing;
        private Stream bmpStream;
        private ImageCodecInfo jpegCodec;
        private EncoderParameters encoderParameters;

        // ImageSharp
        private Image<Rgba32> bmpCore;
        private Image<L8> bmpLuminance;
        private JpegEncoder encoder400;
        private JpegEncoder encoder420;
        private JpegEncoder encoder444;
        private JpegEncoder encoderRgb;

        private MemoryStream destinationStream;

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImage));

                this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
                this.bmpCore.Metadata.ExifProfile = null;
                this.bmpLuminance = this.bmpCore.CloneAs<L8>();
                this.encoder400 = new JpegEncoder { Quality = this.Quality, ColorType = JpegColorType.Luminance };
                this.encoder420 = new JpegEncoder { Quality = this.Quality, ColorType = JpegColorType.YCbCrRatio420 };
                this.encoder444 = new JpegEncoder { Quality = this.Quality, ColorType = JpegColorType.YCbCrRatio444 };
                this.encoderRgb = new JpegEncoder { Quality = this.Quality, ColorType = JpegColorType.Rgb };

                this.bmpStream.Position = 0;
                this.bmpDrawing = SDImage.FromStream(this.bmpStream);
                this.jpegCodec = GetEncoder(ImageFormat.Jpeg);
                this.encoderParameters = new EncoderParameters(1);

                // Quality cast to long is necessary
#pragma warning disable IDE0004 // Remove Unnecessary Cast
                this.encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)this.Quality);
#pragma warning restore IDE0004 // Remove Unnecessary Cast

                this.destinationStream = new MemoryStream();
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bmpStream.Dispose();
            this.bmpStream = null;

            this.destinationStream.Dispose();
            this.destinationStream = null;

            this.bmpCore.Dispose();
            this.bmpDrawing.Dispose();

            this.encoderParameters.Dispose();
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Jpeg 4:2:0")]
        public void JpegSystemDrawing()
        {
            this.bmpDrawing.Save(this.destinationStream, this.jpegCodec, this.encoderParameters);
            this.destinationStream.Seek(0, SeekOrigin.Begin);
        }

        [Benchmark(Description = "ImageSharp (greyscale) Jpeg 4:0:0")]
        public void JpegCore400()
        {
            this.bmpLuminance.SaveAsJpeg(this.destinationStream, this.encoder400);
            this.destinationStream.Seek(0, SeekOrigin.Begin);
        }


        [Benchmark(Description = "ImageSharp Jpeg 4:2:0")]
        public void JpegCore420()
        {
            this.bmpCore.SaveAsJpeg(this.destinationStream, this.encoder420);
            this.destinationStream.Seek(0, SeekOrigin.Begin);
        }

        [Benchmark(Description = "ImageSharp Jpeg 4:4:4")]
        public void JpegCore444()
        {
            this.bmpCore.SaveAsJpeg(this.destinationStream, this.encoder444);
            this.destinationStream.Seek(0, SeekOrigin.Begin);
        }

        [Benchmark(Description = "ImageSharp Jpeg rgb")]
        public void JpegRgb()
        {
            this.bmpCore.SaveAsJpeg(this.destinationStream, this.encoderRgb);
            this.destinationStream.Seek(0, SeekOrigin.Begin);
        }

        // https://docs.microsoft.com/en-us/dotnet/api/system.drawing.imaging.encoderparameter?redirectedfrom=MSDN&view=net-5.0
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }

            return null;
        }
    }
}

/*
BenchmarkDotNet=v0.13.0, OS=linuxmint 20.3
AMD Ryzen 7 5800X, 1 CPU, 16 logical and 8 physical cores
.NET SDK=6.0.200
  [Host]     : .NET Core 3.1.22 (CoreCLR 4.700.21.56803, CoreFX 4.700.21.57101), X64 RyuJIT
  DefaultJob : .NET Core 3.1.22 (CoreCLR 4.700.21.56803, CoreFX 4.700.21.57101), X64 RyuJIT


|                              Method | Quality |      Mean |     Error |    StdDev | Ratio | RatioSD |
|------------------------------------ |-------- |----------:|----------:|----------:|------:|--------:|
|         'System.Drawing Jpeg 4:2:0' |      75 |  9.157 ms | 0.0138 ms | 0.0123 ms |  1.00 |    0.00 |
| 'ImageSharp (greyscale) Jpeg 4:0:0' |      75 | 12.142 ms | 0.1321 ms | 0.1236 ms |  1.33 |    0.01 |
|             'ImageSharp Jpeg 4:2:0' |      75 | 19.655 ms | 0.1057 ms | 0.0883 ms |  2.15 |    0.01 |
|             'ImageSharp Jpeg 4:4:4' |      75 | 19.157 ms | 0.2852 ms | 0.2668 ms |  2.09 |    0.03 |
|               'ImageSharp Jpeg rgb' |      75 | 26.404 ms | 0.3803 ms | 0.3557 ms |  2.89 |    0.04 |
|                                     |         |           |           |           |       |         |
|         'System.Drawing Jpeg 4:2:0' |      90 | 10.828 ms | 0.0727 ms | 0.0680 ms |  1.00 |    0.00 |
| 'ImageSharp (greyscale) Jpeg 4:0:0' |      90 | 14.918 ms | 0.1089 ms | 0.1019 ms |  1.38 |    0.01 |
|             'ImageSharp Jpeg 4:2:0' |      90 | 23.718 ms | 0.0301 ms | 0.0267 ms |  2.19 |    0.02 |
|             'ImageSharp Jpeg 4:4:4' |      90 | 23.857 ms | 0.2387 ms | 0.2233 ms |  2.20 |    0.03 |
|               'ImageSharp Jpeg rgb' |      90 | 34.700 ms | 0.2207 ms | 0.2064 ms |  3.20 |    0.03 |
|                                     |         |           |           |           |       |         |
|         'System.Drawing Jpeg 4:2:0' |     100 | 13.478 ms | 0.0054 ms | 0.0048 ms |  1.00 |    0.00 |
| 'ImageSharp (greyscale) Jpeg 4:0:0' |     100 | 19.446 ms | 0.0803 ms | 0.0751 ms |  1.44 |    0.01 |
|             'ImageSharp Jpeg 4:2:0' |     100 | 30.339 ms | 0.4578 ms | 0.4282 ms |  2.25 |    0.03 |
|             'ImageSharp Jpeg 4:4:4' |     100 | 39.056 ms | 0.1779 ms | 0.1664 ms |  2.90 |    0.01 |
|               'ImageSharp Jpeg rgb' |     100 | 51.828 ms | 0.3336 ms | 0.3121 ms |  3.85 |    0.02 |
*/
