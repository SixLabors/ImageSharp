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
        private JpegEncoder encoder420;
        private JpegEncoder encoder444;

        private MemoryStream destinationStream;

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImage));

                this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
                this.bmpCore.Metadata.ExifProfile = null;
                this.encoder420 = new JpegEncoder { Quality = this.Quality, ColorType = JpegColorType.YCbCrRatio420 };
                this.encoder444 = new JpegEncoder { Quality = this.Quality, ColorType = JpegColorType.YCbCrRatio444 };

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
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19042
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
  [Host]     : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT
  DefaultJob : .NET Core 3.1.18 (CoreCLR 4.700.21.35901, CoreFX 4.700.21.36305), X64 RyuJIT


|                      Method | Quality |     Mean |    Error |   StdDev | Ratio |
|---------------------------- |-------- |---------:|---------:|---------:|------:|
| 'System.Drawing Jpeg 4:2:0' |      75 | 30.04 ms | 0.540 ms | 0.479 ms |  1.00 |
|     'ImageSharp Jpeg 4:2:0' |      75 | 19.32 ms | 0.290 ms | 0.257 ms |  0.64 |
|     'ImageSharp Jpeg 4:4:4' |      75 | 26.76 ms | 0.332 ms | 0.294 ms |  0.89 |
|                             |         |          |          |          |       |
| 'System.Drawing Jpeg 4:2:0' |      90 | 32.82 ms | 0.184 ms | 0.163 ms |  1.00 |
|     'ImageSharp Jpeg 4:2:0' |      90 | 25.00 ms | 0.408 ms | 0.361 ms |  0.76 |
|     'ImageSharp Jpeg 4:4:4' |      90 | 31.83 ms | 0.636 ms | 0.595 ms |  0.97 |
|                             |         |          |          |          |       |
| 'System.Drawing Jpeg 4:2:0' |     100 | 39.30 ms | 0.359 ms | 0.318 ms |  1.00 |
|     'ImageSharp Jpeg 4:2:0' |     100 | 34.49 ms | 0.265 ms | 0.235 ms |  0.88 |
|     'ImageSharp Jpeg 4:4:4' |     100 | 56.40 ms | 0.565 ms | 0.501 ms |  1.44 |
*/
