// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
using System;
using System.Linq;
using System.IO;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.BigTiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Collection("RunSerial")]
    [Trait("Format", "Tiff")]
    [Trait("Format", "BigTiff")]
    public class BigTiffDecoderTests : TiffDecoderBaseTester
    {
        [Theory]
        [WithFile(BigTIFF, PixelTypes.Rgba32)]
        [WithFile(BigTIFFLong, PixelTypes.Rgba32)]
        [WithFile(BigTIFFLong8, PixelTypes.Rgba32)]
        [WithFile(BigTIFFMotorola, PixelTypes.Rgba32)]
        [WithFile(BigTIFFMotorolaLongStrips, PixelTypes.Rgba32)]
        [WithFile(BigTIFFSubIFD4, PixelTypes.Rgba32)]
        [WithFile(BigTIFFSubIFD8, PixelTypes.Rgba32)]
        [WithFile(Indexed4_Deflate, PixelTypes.Rgba32)]
        [WithFile(Indexed8_LZW, PixelTypes.Rgba32)]
        [WithFile(RLE, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(BigTIFFLong8Tiles, PixelTypes.Rgba32)]
        public void ThrowsNotSupported<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<NotSupportedException>(() => provider.GetImage(TiffDecoder));

        [Theory]
        [InlineData(BigTIFF, 24, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(BigTIFFLong, 24, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(BigTIFFLong8, 24, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(BigTIFFMotorola, 24, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(BigTIFFMotorolaLongStrips, 24, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(BigTIFFSubIFD4, 24, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(BigTIFFSubIFD8, 24, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(Indexed4_Deflate, 4, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(Indexed8_LZW, 8, 64, 64, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(RLE, 1, 32, 32, 96, 96, PixelResolutionUnit.PixelsPerInch)]
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

                ImageSharp.Formats.Tiff.TiffMetadata tiffmeta = info.Metadata.GetTiffMetadata();
                Assert.NotNull(tiffmeta);
                Assert.True(tiffmeta.IsBigTiff);
            }
        }

        [Theory]
        [InlineData(BigTIFFLong, ImageSharp.ByteOrder.LittleEndian)]
        [InlineData(BigTIFFMotorola, ImageSharp.ByteOrder.BigEndian)]
        public void ByteOrder(string imagePath, ByteOrder expectedByteOrder)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo info = Image.Identify(stream);

                Assert.NotNull(info.Metadata);
                Assert.Equal(expectedByteOrder, info.Metadata.GetTiffMetadata().ByteOrder);

                stream.Seek(0, SeekOrigin.Begin);

                using var img = Image.Load(stream);
                Assert.Equal(expectedByteOrder, img.Metadata.GetTiffMetadata().ByteOrder);
            }
        }

        [Theory]
        [WithFile(BigTIFFSubIFD8, PixelTypes.Rgba32)]
        public void TiffDecoder_SubIfd8<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage(TiffDecoder);

            ExifProfile meta = image.Frames.RootFrame.Metadata.ExifProfile;

            Assert.Equal(0, meta.InvalidTags.Count);
            Assert.Equal(6, meta.Values.Count);
            Assert.Equal(64, (int)meta.GetValue(ExifTag.ImageWidth).Value);
            Assert.Equal(64, (int)meta.GetValue(ExifTag.ImageLength).Value);
            Assert.Equal(64, (int)meta.GetValue(ExifTag.RowsPerStrip).Value);

            Assert.Equal(1, meta.Values.Count(v => (ushort)v.Tag == (ushort)ExifTagValue.ImageWidth));
            Assert.Equal(1, meta.Values.Count(v => (ushort)v.Tag == (ushort)ExifTagValue.StripOffsets));
            Assert.Equal(1, meta.Values.Count(v => (ushort)v.Tag == (ushort)ExifTagValue.StripByteCounts));
        }
    }
}
