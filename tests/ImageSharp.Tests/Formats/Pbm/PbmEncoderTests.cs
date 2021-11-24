// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Formats.Tga;

using Xunit;
using static SixLabors.ImageSharp.Tests.TestImages.Pbm;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Pbm
{
    [Collection("RunSerial")]
    [Trait("Format", "Pbm")]
    public class PbmEncoderTests
    {
        public static readonly TheoryData<PbmColorType> ColorType =
            new TheoryData<PbmColorType>
            {
                PbmColorType.BlackAndWhite,
                PbmColorType.Grayscale,
                PbmColorType.Rgb
            };

        public static readonly TheoryData<string, PbmColorType> PbmColorTypeFiles =
            new TheoryData<string, PbmColorType>
            {
                { BlackAndWhiteBinary, PbmColorType.BlackAndWhite },
                { BlackAndWhitePlain, PbmColorType.BlackAndWhite },
                { GrayscaleBinary, PbmColorType.Grayscale },
                { GrayscaleBinaryWide, PbmColorType.Grayscale },
                { GrayscalePlain, PbmColorType.Grayscale },
                { RgbBinary, PbmColorType.Rgb },
                { RgbPlain, PbmColorType.Rgb },
            };

        [Theory]
        [MemberData(nameof(PbmColorTypeFiles))]
        public void PbmEncoder_PreserveColorType(string imagePath, PbmColorType pbmColorType)
        {
            var options = new PbmEncoder();

            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);
                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        PbmMetadata meta = output.Metadata.GetPbmMetadata();
                        Assert.Equal(pbmColorType, meta.ColorType);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(PbmColorTypeFiles))]
        public void PbmEncoder_WithPlainEncoding_PreserveBitsPerPixel(string imagePath, PbmColorType pbmColorType)
        {
            var options = new PbmEncoder()
            {
                Encoding = PbmEncoding.Plain
            };

            TestFile testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);
                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        PbmMetadata meta = output.Metadata.GetPbmMetadata();
                        Assert.Equal(pbmColorType, meta.ColorType);
                    }
                }
            }
        }

        [Theory]
        [WithFile(BlackAndWhitePlain, PixelTypes.Rgb24)]
        public void PbmEncoder_P1_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.BlackAndWhite, PbmEncoding.Plain);

        [Theory]
        [WithFile(BlackAndWhiteBinary, PixelTypes.Rgb24)]
        public void PbmEncoder_P4_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.BlackAndWhite, PbmEncoding.Binary);

        /* Disabled as Magick throws an error reading the input image
        [Theory]
        [WithFile(GrayscalePlain, PixelTypes.Rgb24)]
        public void PbmEncoder_P2_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.Grayscale, PbmEncoding.Plain);
        */

        [Theory]
        [WithFile(GrayscaleBinary, PixelTypes.Rgb24)]
        public void PbmEncoder_P5_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.Grayscale, PbmEncoding.Binary);

        /* Disabled as Magick throws an error reading the input image
        [Theory]
        [WithFile(RgbPlain, PixelTypes.Rgb24)]
        public void PbmEncoder_P3_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.Rgb, PbmEncoding.Plain);
        */

        [Theory]
        [WithFile(RgbBinary, PixelTypes.Rgb24)]
        public void PbmEncoder_P6_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.Rgb, PbmEncoding.Binary);

        private static void TestPbmEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            PbmColorType colorType,
            PbmEncoding encoding,
            bool useExactComparer = true,
            float compareTolerance = 0.01f)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var encoder = new PbmEncoder { ColorType = colorType, Encoding = encoding };

                using (var memStream = new MemoryStream())
                {
                    image.Save(memStream, encoder);
                    memStream.Position = 0;
                    using (var encodedImage = (Image<TPixel>)Image.Load(memStream))
                    {
                        TgaTestUtils.CompareWithReferenceDecoder(provider, encodedImage, useExactComparer, compareTolerance);
                    }
                }
            }
        }
    }
}
