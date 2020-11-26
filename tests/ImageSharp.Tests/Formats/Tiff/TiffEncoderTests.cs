// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    public class TiffEncoderTests
    {
        public static readonly TheoryData<string, TiffBitsPerPixel> TiffBitsPerPixelFiles =
            new TheoryData<string, TiffBitsPerPixel>
            {
                { TestImages.Tiff.GrayscaleUncompressed, TiffBitsPerPixel.Pixel8 },
                { TestImages.Tiff.RgbUncompressed, TiffBitsPerPixel.Pixel24 },
            };

        [Theory]
        [MemberData(nameof(TiffBitsPerPixelFiles))]
        public void TiffEncoder_PreserveBitsPerPixel(string imagePath, TiffBitsPerPixel expectedBitsPerPixel)
        {
            // arrange
            var tiffEncoder = new TiffEncoder();
            var testFile = TestFile.Create(imagePath);
            using Image<Rgba32> input = testFile.CreateRgba32Image();
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, tiffEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            TiffMetadata meta = output.Metadata.GetTiffMetadata();
            Assert.Equal(expectedBitsPerPixel, meta.BitsPerPixel);
        }

        [Theory]
        [WithFile(TestImages.Tiff.RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_Works<TPixel>(TestImageProvider<TPixel> provider, TiffBitsPerPixel bitsPerPixel = TiffBitsPerPixel.Pixel24)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, bitsPerPixel);

        [Theory]
        [WithFile(TestImages.Tiff.RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_Works<TPixel>(TestImageProvider<TPixel> provider, TiffBitsPerPixel bitsPerPixel = TiffBitsPerPixel.Pixel8)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, bitsPerPixel);

        [Theory]
        [WithFile(TestImages.Tiff.RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_Works<TPixel>(TestImageProvider<TPixel> provider, TiffBitsPerPixel bitsPerPixel = TiffBitsPerPixel.Pixel8, bool useColorPalette = true)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, bitsPerPixel, useColorPalette);

        private static void TestTiffEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            TiffBitsPerPixel bitsPerPixel,
            bool useColorPalette = false,
            TiffEncoderCompression compression = TiffEncoderCompression.None,
            bool useExactComparer = true,
            float compareTolerance = 0.01f)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            var encoder = new TiffEncoder { BitsPerPixel = bitsPerPixel, Compression = compression };

            using var memStream = new MemoryStream();
            image.Save(memStream, encoder);
            memStream.Position = 0;
            using var encodedImage = (Image<TPixel>)Image.Load(memStream);
            TiffTestUtils.CompareWithReferenceDecoder(provider, encodedImage, useExactComparer, compareTolerance);
        }
    }
}
