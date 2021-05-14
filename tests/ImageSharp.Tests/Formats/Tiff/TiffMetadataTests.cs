// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;

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
                BitsPerPixel = TiffBitsPerPixel.Bit8,
                ByteOrder = ByteOrder.BigEndian,
            };

            var clone = (TiffMetadata)meta.DeepClone();

            clone.BitsPerPixel = TiffBitsPerPixel.Bit24;
            clone.ByteOrder = ByteOrder.LittleEndian;

            Assert.False(meta.BitsPerPixel == clone.BitsPerPixel);
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

                clone.BitsPerSample = TiffBitsPerSample.Bit1;
                clone.ColorMap = new ushort[] { 1, 2, 3 };

                Assert.False(meta.BitsPerSample == clone.BitsPerSample);
                Assert.False(meta.ColorMap.SequenceEqual(clone.ColorMap));
            }
        }

        [Theory]
        [InlineData(Calliphora_BiColorUncompressed, TiffBitsPerPixel.Bit1)]
        [InlineData(GrayscaleUncompressed, TiffBitsPerPixel.Bit8)]
        [InlineData(RgbUncompressed, TiffBitsPerPixel.Bit24)]
        public void Identify_DetectsCorrectBitPerPixel(string imagePath, TiffBitsPerPixel expectedBitsPerPixel)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            IImageInfo imageInfo = Image.Identify(this.configuration, stream);

            Assert.NotNull(imageInfo);
            TiffMetadata tiffMetadata = imageInfo.Metadata.GetTiffMetadata();
            Assert.NotNull(tiffMetadata);
            Assert.Equal(expectedBitsPerPixel, tiffMetadata.BitsPerPixel);
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
                    Assert.Equal(30, rootFrameMetaData.ExifProfile.Values.Count);
                }
            }
        }

        [Theory]
        [WithFile(InvalidIptcData, PixelTypes.Rgba32)]
        public void CanDecodeImage_WithIptcDataAsLong<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage(TiffDecoder);

            Assert.NotNull(image.Metadata.IptcProfile);
            IptcValue byline = image.Metadata.IptcProfile.Values.FirstOrDefault(data => data.Tag == IptcTag.Byline);
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
                Assert.NotNull(exifProfile);
                Assert.Equal(30, exifProfile.Values.Count);
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

                TiffFrameMetadata tiffFrameMetadata = rootFrame.Metadata.GetTiffMetadata();
                Assert.NotNull(tiffFrameMetadata);

                ImageMetadata imageMetaData = image.Metadata;
                Assert.NotNull(imageMetaData);
                Assert.Equal(PixelResolutionUnit.PixelsPerInch, imageMetaData.ResolutionUnits);
                Assert.Equal(10, imageMetaData.HorizontalResolution);
                Assert.Equal(10, imageMetaData.VerticalResolution);

                TiffMetadata tiffMetaData = image.Metadata.GetTiffMetadata();
                Assert.NotNull(tiffMetaData);
                Assert.Equal(ByteOrder.LittleEndian, tiffMetaData.ByteOrder);
                Assert.Equal(TiffBitsPerPixel.Bit4, tiffMetaData.BitsPerPixel);

                VerifyExpectedTiffFrameMetaDataIsPresent(tiffFrameMetadata);
            }
        }

        private static void VerifyExpectedTiffFrameMetaDataIsPresent(TiffFrameMetadata frameMetaData)
        {
            Assert.Equal(TiffBitsPerSample.Bit4, frameMetaData.BitsPerSample);
            Assert.Equal(TiffCompression.Lzw, frameMetaData.Compression);
            Assert.Equal(TiffPhotometricInterpretation.PaletteColor, frameMetaData.PhotometricInterpretation);
            Assert.Equal(new Number[] { 8u }, frameMetaData.StripOffsets, new NumberComparer());
            Assert.Equal(1, frameMetaData.SamplesPerPixel.GetValueOrDefault());
            Assert.Equal(32u, frameMetaData.RowsPerStrip);
            Assert.Equal(new Number[] { 297u }, frameMetaData.StripByteCounts, new NumberComparer());
            Assert.Equal(PixelResolutionUnit.PixelsPerInch, frameMetaData.ResolutionUnit);
            Assert.Equal(10, frameMetaData.HorizontalResolution);
            Assert.Equal(10, frameMetaData.VerticalResolution);
            Assert.Equal(TiffPlanarConfiguration.Chunky, frameMetaData.PlanarConfiguration);
            Assert.Equal(48, frameMetaData.ColorMap.Length);
            Assert.Equal(10537, frameMetaData.ColorMap[0]);
            Assert.Equal(14392, frameMetaData.ColorMap[1]);
            Assert.Equal(58596, frameMetaData.ColorMap[46]);
            Assert.Equal(3855, frameMetaData.ColorMap[47]);

            Assert.Null(frameMetaData.ExtraSamples);
            Assert.Equal(TiffPredictor.None, frameMetaData.Predictor);
            Assert.Null(frameMetaData.SampleFormat);
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

                TiffFrameMetadata frame0MetaData = image.Frames[0].Metadata.GetTiffMetadata();
                Assert.Equal(TiffNewSubfileType.FullImage, frame0MetaData.SubfileType);
                Assert.Equal(255, image.Frames[0].Width);
                Assert.Equal(255, image.Frames[0].Height);

                TiffFrameMetadata frame1MetaData = image.Frames[1].Metadata.GetTiffMetadata();
                Assert.Equal(TiffNewSubfileType.Preview, frame1MetaData.SubfileType);
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
            TiffMetadata tiffMetaInput = image.Metadata.GetTiffMetadata();
            TiffFrameMetadata frameMetaInput = image.Frames.RootFrame.Metadata.GetTiffMetadata();
            ImageFrame<TPixel> rootFrameInput = image.Frames.RootFrame;
            byte[] xmpProfileInput = rootFrameInput.Metadata.XmpProfile;
            ExifProfile rootFrameExifProfileInput = rootFrameInput.Metadata.ExifProfile;

            Assert.Equal(TiffCompression.Lzw, frameMetaInput.Compression);
            Assert.Equal(TiffBitsPerPixel.Bit4, tiffMetaInput.BitsPerPixel);

            // Save to Tiff
            var tiffEncoder = new TiffEncoder() { Mode = TiffEncodingMode.Rgb };
            using var ms = new MemoryStream();
            image.Save(ms, tiffEncoder);

            // Assert
            ms.Position = 0;
            using var encodedImage = Image.Load<Rgba32>(this.configuration, ms);

            ImageMetadata encodedImageMetaData = encodedImage.Metadata;
            TiffMetadata tiffMetaDataEncodedImage = encodedImageMetaData.GetTiffMetadata();
            ImageFrame<Rgba32> rootFrameEncodedImage = encodedImage.Frames.RootFrame;
            TiffFrameMetadata tiffMetaDataEncodedRootFrame = rootFrameEncodedImage.Metadata.GetTiffMetadata();
            ExifProfile encodedImageExifProfile = rootFrameEncodedImage.Metadata.ExifProfile;
            byte[] encodedImageXmpProfile = rootFrameEncodedImage.Metadata.XmpProfile;

            Assert.Equal(TiffBitsPerPixel.Bit24, tiffMetaDataEncodedImage.BitsPerPixel);
            Assert.Equal(TiffCompression.None, tiffMetaDataEncodedRootFrame.Compression);

            Assert.Equal(inputMetaData.HorizontalResolution, encodedImageMetaData.HorizontalResolution);
            Assert.Equal(inputMetaData.VerticalResolution, encodedImageMetaData.VerticalResolution);
            Assert.Equal(inputMetaData.ResolutionUnits, encodedImageMetaData.ResolutionUnits);

            Assert.Equal(rootFrameInput.Width, rootFrameEncodedImage.Width);
            Assert.Equal(rootFrameInput.Height, rootFrameEncodedImage.Height);
            Assert.Equal(frameMetaInput.ResolutionUnit, tiffMetaDataEncodedRootFrame.ResolutionUnit);
            Assert.Equal(frameMetaInput.HorizontalResolution, tiffMetaDataEncodedRootFrame.HorizontalResolution);
            Assert.Equal(frameMetaInput.VerticalResolution, tiffMetaDataEncodedRootFrame.VerticalResolution);

            Assert.Equal(xmpProfileInput, encodedImageXmpProfile);

            Assert.Equal("IrfanView", rootFrameExifProfileInput.GetValue(ExifTag.Software).Value);
            Assert.Equal("This is Название", rootFrameExifProfileInput.GetValue(ExifTag.ImageDescription).Value);
            Assert.Equal("This is Изготовитель камеры", rootFrameExifProfileInput.GetValue(ExifTag.Make).Value);
            Assert.Equal("This is Авторские права", rootFrameExifProfileInput.GetValue(ExifTag.Copyright).Value);

            Assert.Equal(rootFrameExifProfileInput.Values.Count, encodedImageExifProfile.Values.Count);
            Assert.Equal(rootFrameExifProfileInput.GetValue(ExifTag.ImageDescription).Value, encodedImageExifProfile.GetValue(ExifTag.ImageDescription).Value);
            Assert.Equal(rootFrameExifProfileInput.GetValue(ExifTag.Make).Value, encodedImageExifProfile.GetValue(ExifTag.Make).Value);
            Assert.Equal(rootFrameExifProfileInput.GetValue(ExifTag.Copyright).Value, encodedImageExifProfile.GetValue(ExifTag.Copyright).Value);
        }
    }
}
