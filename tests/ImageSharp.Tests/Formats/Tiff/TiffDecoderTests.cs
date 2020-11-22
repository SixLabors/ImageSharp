// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Category", "Tiff.BlackBox.Decoder")]
    [Trait("Category", "Tiff")]
    public class TiffDecoderTests
    {
        private static TiffDecoder TiffDecoder => new TiffDecoder();

        private static MagickReferenceDecoder ReferenceDecoder => new MagickReferenceDecoder();

        public static readonly string[] SingleTestImages = TestImages.Tiff.All;

        public static readonly string[] MultiframeTestImages = TestImages.Tiff.Multiframes;

        public static readonly string[] NotSupportedImages = TestImages.Tiff.NotSupported;

        [Theory]
        [WithFileCollection(nameof(NotSupportedImages), PixelTypes.Rgba32)]
        public void ThrowsNotSupported<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            Assert.Throws<NotSupportedException>(() => provider.GetImage(TiffDecoder));
        }

        [Theory]
        [InlineData(TestImages.Tiff.RgbUncompressed, 24, 256, 256, 300, 300, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(TestImages.Tiff.SmallRgbDeflate, 24, 32, 32, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(TestImages.Tiff.Calliphora_GrayscaleUncompressed, 8, 804, 1198, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        public void Identify(string imagePath, int expectedPixelSize, int expectedWidth, int expectedHeight, double expectedHResolution, double expectedVResolution, PixelResolutionUnit expectedResolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo info = Image.Identify(stream);

                Assert.Equal(expectedPixelSize, info.PixelType?.BitsPerPixel);
                Assert.Equal(expectedWidth, info.Width);
                Assert.Equal(expectedHeight, info.Height);
                Assert.NotNull(info.Metadata);
                Assert.Equal(expectedHResolution, info.Metadata.HorizontalResolution);
                Assert.Equal(expectedVResolution, info.Metadata.VerticalResolution);
                Assert.Equal(expectedResolutionUnit, info.Metadata.ResolutionUnits);
            }
        }

        [Theory]
        [InlineData(TestImages.Tiff.RgbLzw_NoPredictor_Multistrip, TiffByteOrder.LittleEndian)]
        [InlineData(TestImages.Tiff.RgbLzw_NoPredictor_Multistrip_Motorola, TiffByteOrder.BigEndian)]
        public void ByteOrder(string imagePath, TiffByteOrder expectedByteOrder)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo info = Image.Identify(stream);

                Assert.NotNull(info.Metadata);
                Assert.Equal(expectedByteOrder, info.Metadata.GetTiffMetadata().ByteOrder);

                // todo: it's not a mistake?
                stream.Seek(0, SeekOrigin.Begin);

                using var img = Image.Load(stream);
                Assert.Equal(expectedByteOrder, img.Metadata.GetTiffMetadata().ByteOrder);
            }
        }

        [Theory]
        [WithFileCollection(nameof(SingleTestImages), PixelTypes.Rgba32)]
        public void Decode<TPixel>(TestImageProvider<TPixel> provider)
          where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TiffDecoder))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
            }
        }

        [Theory]
        [WithFileCollection(nameof(MultiframeTestImages), PixelTypes.Rgba32)]
        public void DecodeMultiframe<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TiffDecoder))
            {
                Assert.True(image.Frames.Count > 1);

                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);

                image.DebugSaveMultiFrame(provider);
                image.CompareToOriginalMultiFrame(provider, ImageComparer.Exact, ReferenceDecoder);
            }
        }
    }
}
