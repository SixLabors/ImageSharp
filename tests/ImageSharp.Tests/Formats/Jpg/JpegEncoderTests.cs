// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.



// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System.Collections.Generic;
    using System.IO;

    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using SixLabors.Primitives;

    using Xunit;
    using Xunit.Abstractions;

    public class JpegEncoderTests : MeasureFixture
    {
        public static IEnumerable<string> AllBmpFiles => TestImages.Bmp.All;

        public JpegEncoderTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Snake, PixelTypes.Rgba32, 75, JpegSubsample.Ratio420)]
        [WithFile(TestImages.Jpeg.Baseline.Lake, PixelTypes.Rgba32, 75, JpegSubsample.Ratio420)]
        [WithFile(TestImages.Jpeg.Baseline.Snake, PixelTypes.Rgba32, 75, JpegSubsample.Ratio444)]
        [WithFile(TestImages.Jpeg.Baseline.Lake, PixelTypes.Rgba32, 75, JpegSubsample.Ratio444)]
        public void LoadResizeSave<TPixel>(TestImageProvider<TPixel> provider, int quality, JpegSubsample subsample)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(x=>x.Resize(new ResizeOptions { Size = new Size(150, 100), Mode = ResizeMode.Max })))
            {

                image.MetaData.ExifProfile = null; // Reduce the size of the file
                JpegEncoder options = new JpegEncoder { Subsample = subsample, Quality = quality };

                provider.Utility.TestName += $"{subsample}_Q{quality}";
                provider.Utility.SaveTestOutputFile(image, "png");
                provider.Utility.SaveTestOutputFile(image, "jpg", options);
            }
        }

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgba32 | PixelTypes.Rgba32 | PixelTypes.Argb32, JpegSubsample.Ratio420, 75)]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgba32 | PixelTypes.Rgba32 | PixelTypes.Argb32, JpegSubsample.Ratio444, 75)]
        public void OpenBmp_SaveJpeg<TPixel>(TestImageProvider<TPixel> provider, JpegSubsample subSample, int quality)
           where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                ImagingTestCaseUtility utility = provider.Utility;
                utility.TestName += "_" + subSample + "_Q" + quality;

                using (FileStream outputStream = File.OpenWrite(utility.GetTestOutputFileName("jpg")))
                {
                    image.Save(outputStream, new JpegEncoder()
                    {
                      Subsample = subSample,
                      Quality = quality
                    });
                }
            }
        }

        [Fact]
        public void Encode_IgnoreMetadataIsFalse_ExifProfileIsWritten()
        {
            JpegEncoder options = new JpegEncoder()
            {
                IgnoreMetadata = false
            };

            TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan);

            using (Image<Rgba32> input = testFile.CreateImage())
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    input.Save(memStream,  options);

                    memStream.Position = 0;
                    using (Image<Rgba32> output = Image.Load<Rgba32>(memStream))
                    {
                        Assert.NotNull(output.MetaData.ExifProfile);
                    }
                }
            }
        }

        [Fact]
        public void Encode_IgnoreMetadataIsTrue_ExifProfileIgnored()
        {
            JpegEncoder options = new JpegEncoder()
            {
                IgnoreMetadata = true
            };

            TestFile testFile = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan);

            using (Image<Rgba32> input = testFile.CreateImage())
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    input.SaveAsJpeg(memStream, options);

                    memStream.Position = 0;
                    using (Image<Rgba32> output = Image.Load<Rgba32>(memStream))
                    {
                        Assert.Null(output.MetaData.ExifProfile);
                    }
                }
            }
        }
    }
}