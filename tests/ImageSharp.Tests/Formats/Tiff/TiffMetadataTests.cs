// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    public class TiffMetadataTests
    {
        private readonly Configuration configuration;

        private static TiffDecoder TiffDecoder => new TiffDecoder();

        public TiffMetadataTests()
        {
            this.configuration = new Configuration();
            this.configuration.AddTiff();
        }

        [Fact]
        public void TiffMetadata_CloneIsDeep()
        {
            var meta = new TiffMetadata
            {
                ByteOrder = ByteOrder.BigEndian,
            };

            var clone = (TiffMetadata)meta.DeepClone();

            clone.ByteOrder = ByteOrder.LittleEndian;

            Assert.False(meta.ByteOrder == clone.ByteOrder);
        }

        [Theory]
        [WithFile(SampleMetadata, PixelTypes.Rgba32)]
        public void TiffFrameMetadata_CloneIsDeep<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TiffDecoder))
            {
                TiffFrameMetadata meta = image.Frames.RootFrame.Metadata.GetTiffMetadata();
                var cloneSameAsSampleMetaData = (TiffFrameMetadata)meta.DeepClone();
                VerifyExpectedTiffFrameMetaDataIsPresent(cloneSameAsSampleMetaData);

                var clone = (TiffFrameMetadata)meta.DeepClone();

                clone.BitsPerPixel = TiffBitsPerPixel.Bit8;
                clone.Compression = TiffCompression.None;
                clone.PhotometricInterpretation = TiffPhotometricInterpretation.CieLab;
                clone.Predictor = TiffPredictor.Horizontal;

                Assert.False(meta.BitsPerPixel == clone.BitsPerPixel);
                Assert.False(meta.Compression == clone.Compression);
                Assert.False(meta.PhotometricInterpretation == clone.PhotometricInterpretation);
                Assert.False(meta.Predictor == clone.Predictor);
            }
        }

        private static void VerifyExpectedTiffFrameMetaDataIsPresent(TiffFrameMetadata frameMetaData)
        {
            Assert.NotNull(frameMetaData);
            Assert.NotNull(frameMetaData.BitsPerPixel);
            Assert.Equal(TiffBitsPerSample.Bit4, (TiffBitsPerSample)frameMetaData.BitsPerPixel);
            Assert.Equal(TiffCompression.Lzw, frameMetaData.Compression);
            Assert.Equal(TiffPhotometricInterpretation.PaletteColor, frameMetaData.PhotometricInterpretation);
            Assert.Equal(TiffPredictor.None, frameMetaData.Predictor);
        }

        [Theory]
        [InlineData(Calliphora_BiColorUncompressed, 1)]
        [InlineData(GrayscaleUncompressed, 8)]
        [InlineData(RgbUncompressed, 24)]
        public void Identify_DetectsCorrectBitPerPixel(string imagePath, int expectedBitsPerPixel)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            IImageInfo imageInfo = Image.Identify(this.configuration, stream);

            Assert.NotNull(imageInfo);
            TiffMetadata tiffMetadata = imageInfo.Metadata.GetTiffMetadata();
            Assert.NotNull(tiffMetadata);
            Assert.Equal(expectedBitsPerPixel, imageInfo.PixelType.BitsPerPixel);
        }

        [Theory]
        [InlineData(GrayscaleUncompressed, ByteOrder.BigEndian)]
        [InlineData(LittleEndianByteOrder, ByteOrder.LittleEndian)]
        public void Identify_DetectsCorrectByteOrder(string imagePath, ByteOrder expectedByteOrder)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            IImageInfo imageInfo = Image.Identify(this.configuration, stream);

            Assert.NotNull(imageInfo);
            TiffMetadata tiffMetadata = imageInfo.Metadata.GetTiffMetadata();
            Assert.NotNull(tiffMetadata);
            Assert.Equal(expectedByteOrder, tiffMetadata.ByteOrder);
        }

        [Theory]
        [WithFile(SampleMetadata, PixelTypes.Rgba32, false)]
        [WithFile(SampleMetadata, PixelTypes.Rgba32, true)]
        public void MetadataProfiles<TPixel>(TestImageProvider<TPixel> provider, bool ignoreMetadata)
          where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TiffDecoder() { IgnoreMetadata = ignoreMetadata }))
            {
                TiffMetadata meta = image.Metadata.GetTiffMetadata();
                ImageFrameMetadata rootFrameMetaData = image.Frames.RootFrame.Metadata;
                Assert.NotNull(meta);
                if (ignoreMetadata)
                {
                    Assert.Null(rootFrameMetaData.XmpProfile);
                    Assert.Null(rootFrameMetaData.ExifProfile);
                }
                else
                {
                    Assert.NotNull(rootFrameMetaData.XmpProfile);
                    Assert.NotNull(rootFrameMetaData.ExifProfile);
                    Assert.Equal(2599, rootFrameMetaData.XmpProfile.Length);
                    Assert.Equal(27, rootFrameMetaData.ExifProfile.Values.Count);
                }
            }
        }

        [Theory]
        [WithFile(InvalidIptcData, PixelTypes.Rgba32)]
        public void CanDecodeImage_WithIptcDataAsLong<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage(TiffDecoder);

            IptcProfile iptcProfile = image.Frames.RootFrame.Metadata.IptcProfile;
            Assert.NotNull(iptcProfile);
            IptcValue byline = iptcProfile.Values.FirstOrDefault(data => data.Tag == IptcTag.Byline);
            Assert.NotNull(byline);
            Assert.Equal("Studio Mantyniemi", byline.Value);
        }

        [Theory]
        [WithFile(SampleMetadata, PixelTypes.Rgba32)]
        public void BaselineTags<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TiffDecoder))
            {
                ImageFrame<TPixel> rootFrame = image.Frames.RootFrame;
                Assert.Equal(32, rootFrame.Width);
                Assert.Equal(32, rootFrame.Height);
                Assert.NotNull(rootFrame.Metadata.XmpProfile);
                Assert.Equal(2599, rootFrame.Metadata.XmpProfile.Length);

                ExifProfile exifProfile = rootFrame.Metadata.ExifProfile;
                TiffFrameMetadata tiffFrameMetadata = rootFrame.Metadata.GetTiffMetadata();
                Assert.NotNull(exifProfile);

                // The original exifProfile has 30 values, but 3 of those values will be stored in the TiffFrameMetaData
                // and removed from the profile on decode.
                Assert.Equal(27, exifProfile.Values.Count);
                Assert.Equal(TiffCompression.Lzw, tiffFrameMetadata.Compression);
                Assert.Equal("This is Название", exifProfile.GetValue(ExifTag.ImageDescription).Value);
                Assert.Equal("This is Изготовитель камеры", exifProfile.GetValue(ExifTag.Make).Value);
                Assert.Equal("This is Модель камеры", exifProfile.GetValue(ExifTag.Model).Value);
                Assert.Equal("IrfanView", exifProfile.GetValue(ExifTag.Software).Value);
                Assert.Null(exifProfile.GetValue(ExifTag.DateTime)?.Value);
                Assert.Equal("This is author1;Author2", exifProfile.GetValue(ExifTag.Artist).Value);
                Assert.Null(exifProfile.GetValue(ExifTag.HostComputer)?.Value);
                Assert.Equal("This is Авторские права", exifProfile.GetValue(ExifTag.Copyright).Value);
                Assert.Equal(4, exifProfile.GetValue(ExifTag.Rating).Value);
                Assert.Equal(75, exifProfile.GetValue(ExifTag.RatingPercent).Value);
                var expectedResolution = new Rational(10000, 1000, simplify: false);
                Assert.Equal(expectedResolution, exifProfile.GetValue(ExifTag.XResolution).Value);
                Assert.Equal(expectedResolution, exifProfile.GetValue(ExifTag.YResolution).Value);
                Assert.Equal(new Number[] { 8u }, exifProfile.GetValue(ExifTag.StripOffsets)?.Value, new NumberComparer());
                Assert.Equal(new Number[] { 297u }, exifProfile.GetValue(ExifTag.StripByteCounts)?.Value, new NumberComparer());
                Assert.Null(exifProfile.GetValue(ExifTag.ExtraSamples)?.Value);
                Assert.Equal(32u, exifProfile.GetValue(ExifTag.RowsPerStrip).Value);
                Assert.Null(exifProfile.GetValue(ExifTag.SampleFormat));
                Assert.Equal(TiffPredictor.None, tiffFrameMetadata.Predictor);
                Assert.Equal(PixelResolutionUnit.PixelsPerInch, UnitConverter.ExifProfileToResolutionUnit(exifProfile));
                ushort[] colorMap = exifProfile.GetValue(ExifTag.ColorMap)?.Value;
                Assert.NotNull(colorMap);
                Assert.Equal(48, colorMap.Length);
                Assert.Equal(10537, colorMap[0]);
                Assert.Equal(14392, colorMap[1]);
                Assert.Equal(58596, colorMap[46]);
                Assert.Equal(3855, colorMap[47]);
                Assert.Equal(TiffPhotometricInterpretation.PaletteColor, tiffFrameMetadata.PhotometricInterpretation);
                Assert.Equal(1u, exifProfile.GetValue(ExifTag.SamplesPerPixel).Value);

                ImageMetadata imageMetaData = image.Metadata;
                Assert.NotNull(imageMetaData);
                Assert.Equal(PixelResolutionUnit.PixelsPerInch, imageMetaData.ResolutionUnits);
                Assert.Equal(10, imageMetaData.HorizontalResolution);
                Assert.Equal(10, imageMetaData.VerticalResolution);

                TiffMetadata tiffMetaData = image.Metadata.GetTiffMetadata();
                Assert.NotNull(tiffMetaData);
                Assert.Equal(ByteOrder.LittleEndian, tiffMetaData.ByteOrder);

                var frameMetaData = TiffFrameMetadata.Parse(exifProfile);
                Assert.Equal(TiffBitsPerPixel.Bit4, frameMetaData.BitsPerPixel);
            }
        }

        [Theory]
        [WithFile(MultiframeDeflateWithPreview, PixelTypes.Rgba32)]
        public void SubfileType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(TiffDecoder))
            {
                TiffMetadata meta = image.Metadata.GetTiffMetadata();
                Assert.NotNull(meta);

                Assert.Equal(2, image.Frames.Count);

                ExifProfile frame0Exif = image.Frames[0].Metadata.ExifProfile;
                Assert.Equal(TiffNewSubfileType.FullImage, (TiffNewSubfileType)frame0Exif.GetValue(ExifTag.SubfileType).Value);
                Assert.Equal(255, image.Frames[0].Width);
                Assert.Equal(255, image.Frames[0].Height);

                ExifProfile frame1Exif = image.Frames[1].Metadata.ExifProfile;
                Assert.Equal(TiffNewSubfileType.Preview, (TiffNewSubfileType)frame1Exif.GetValue(ExifTag.SubfileType).Value);
                Assert.Equal(255, image.Frames[1].Width);
                Assert.Equal(255, image.Frames[1].Height);
            }
        }

        [Theory]
        [WithFile(SampleMetadata, PixelTypes.Rgba32)]
        public void Encode_PreservesMetadata<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Load Tiff image
            using Image<TPixel> image = provider.GetImage(new TiffDecoder() { IgnoreMetadata = false });

            ImageMetadata inputMetaData = image.Metadata;
            ImageFrame<TPixel> rootFrameInput = image.Frames.RootFrame;
            TiffFrameMetadata frameMetaInput = rootFrameInput.Metadata.GetTiffMetadata();
            byte[] xmpProfileInput = rootFrameInput.Metadata.XmpProfile;
            ExifProfile exifProfileInput = rootFrameInput.Metadata.ExifProfile;

            Assert.Equal(TiffCompression.Lzw, frameMetaInput.Compression);
            Assert.Equal(TiffBitsPerPixel.Bit4, frameMetaInput.BitsPerPixel);

            // Save to Tiff
            var tiffEncoder = new TiffEncoder() { PhotometricInterpretation = TiffPhotometricInterpretation.Rgb };
            using var ms = new MemoryStream();
            image.Save(ms, tiffEncoder);

            // Assert
            ms.Position = 0;
            using var encodedImage = Image.Load<Rgba32>(this.configuration, ms);

            ImageMetadata encodedImageMetaData = encodedImage.Metadata;
            ImageFrame<Rgba32> rootFrameEncodedImage = encodedImage.Frames.RootFrame;
            TiffFrameMetadata tiffMetaDataEncodedRootFrame = rootFrameEncodedImage.Metadata.GetTiffMetadata();
            ExifProfile encodedImageExifProfile = rootFrameEncodedImage.Metadata.ExifProfile;
            byte[] encodedImageXmpProfile = rootFrameEncodedImage.Metadata.XmpProfile;

            Assert.Equal(TiffBitsPerPixel.Bit4, tiffMetaDataEncodedRootFrame.BitsPerPixel);
            Assert.Equal(TiffCompression.Lzw, tiffMetaDataEncodedRootFrame.Compression);

            Assert.Equal(inputMetaData.HorizontalResolution, encodedImageMetaData.HorizontalResolution);
            Assert.Equal(inputMetaData.VerticalResolution, encodedImageMetaData.VerticalResolution);
            Assert.Equal(inputMetaData.ResolutionUnits, encodedImageMetaData.ResolutionUnits);

            Assert.Equal(rootFrameInput.Width, rootFrameEncodedImage.Width);
            Assert.Equal(rootFrameInput.Height, rootFrameEncodedImage.Height);

            PixelResolutionUnit resolutionUnitInput = UnitConverter.ExifProfileToResolutionUnit(exifProfileInput);
            PixelResolutionUnit resolutionUnitEncoded = UnitConverter.ExifProfileToResolutionUnit(encodedImageExifProfile);
            Assert.Equal(resolutionUnitInput, resolutionUnitEncoded);
            Assert.Equal(exifProfileInput.GetValue(ExifTag.XResolution), encodedImageExifProfile.GetValue(ExifTag.XResolution));
            Assert.Equal(exifProfileInput.GetValue(ExifTag.YResolution), encodedImageExifProfile.GetValue(ExifTag.YResolution));

            Assert.Equal(xmpProfileInput, encodedImageXmpProfile);

            Assert.Equal("IrfanView", exifProfileInput.GetValue(ExifTag.Software).Value);
            Assert.Equal("This is Название", exifProfileInput.GetValue(ExifTag.ImageDescription).Value);
            Assert.Equal("This is Изготовитель камеры", exifProfileInput.GetValue(ExifTag.Make).Value);
            Assert.Equal("This is Авторские права", exifProfileInput.GetValue(ExifTag.Copyright).Value);

            Assert.Equal(exifProfileInput.Values.Count, encodedImageExifProfile.Values.Count);
            Assert.Equal(exifProfileInput.GetValue(ExifTag.ImageDescription).Value, encodedImageExifProfile.GetValue(ExifTag.ImageDescription).Value);
            Assert.Equal(exifProfileInput.GetValue(ExifTag.Make).Value, encodedImageExifProfile.GetValue(ExifTag.Make).Value);
            Assert.Equal(exifProfileInput.GetValue(ExifTag.Copyright).Value, encodedImageExifProfile.GetValue(ExifTag.Copyright).Value);
        }
    }
}
