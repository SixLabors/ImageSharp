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
                this.encoder420 = new JpegEncoder { Quality = this.Quality, Subsample = JpegSubsample.Ratio420 };
                this.encoder444 = new JpegEncoder { Quality = this.Quality, Subsample = JpegSubsample.Ratio444 };

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
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=6.0.100-preview.3.21202.5
  [Host]     : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT  [AttachedDebugger]
  DefaultJob : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT


|                      Method | Quality |     Mean |    Error |   StdDev | Ratio | RatioSD |
|---------------------------- |-------- |---------:|---------:|---------:|------:|--------:|
| 'System.Drawing Jpeg 4:2:0' |      75 | 30.60 ms | 0.496 ms | 0.464 ms |  1.00 |    0.00 |
|     'ImageSharp Jpeg 4:2:0' |      75 | 29.86 ms | 0.350 ms | 0.311 ms |  0.98 |    0.02 |
|     'ImageSharp Jpeg 4:4:4' |      75 | 45.36 ms | 0.899 ms | 1.036 ms |  1.48 |    0.05 |
|                             |         |          |          |          |       |         |
| 'System.Drawing Jpeg 4:2:0' |      90 | 34.05 ms | 0.669 ms | 0.687 ms |  1.00 |    0.00 |
|     'ImageSharp Jpeg 4:2:0' |      90 | 37.26 ms | 0.706 ms | 0.660 ms |  1.10 |    0.03 |
|     'ImageSharp Jpeg 4:4:4' |      90 | 52.54 ms | 0.579 ms | 0.514 ms |  1.55 |    0.04 |
|                             |         |          |          |          |       |         |
| 'System.Drawing Jpeg 4:2:0' |     100 | 39.36 ms | 0.267 ms | 0.237 ms |  1.00 |    0.00 |
|     'ImageSharp Jpeg 4:2:0' |     100 | 42.44 ms | 0.410 ms | 0.383 ms |  1.08 |    0.01 |
|     'ImageSharp Jpeg 4:4:4' |     100 | 70.88 ms | 0.508 ms | 0.450 ms |  1.80 |    0.02 |
*/
