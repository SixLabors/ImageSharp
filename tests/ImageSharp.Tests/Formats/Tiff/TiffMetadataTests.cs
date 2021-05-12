// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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
            byte[] xmpData = { 1, 1, 1 };
            var meta = new TiffMetadata
            {
                BitsPerPixel = TiffBitsPerPixel.Bit8,
                ByteOrder = ByteOrder.BigEndian,
                XmpProfile = xmpData
            };

            var clone = (TiffMetadata)meta.DeepClone();

            clone.BitsPerPixel = TiffBitsPerPixel.Bit24;
            clone.ByteOrder = ByteOrder.LittleEndian;

            Assert.False(meta.BitsPerPixel == clone.BitsPerPixel);
            Assert.False(meta.ByteOrder == clone.ByteOrder);
            Assert.False(meta.XmpProfile.Equals(clone.XmpProfile));
            Assert.True(meta.XmpProfile.SequenceEqual(clone.XmpProfile));
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
                VerifyExpectedFrameMetaDataIsPresent(cloneSameAsSampleMetaData);

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
                Assert.NotNull(meta);
                if (ignoreMetadata)
                {
                    Assert.Null(meta.XmpProfile);
                }
                else
                {
                    Assert.NotNull(meta.XmpProfile);
                    Assert.Equal(2599, meta.XmpProfile.Length);
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

                TiffFrameMetadata frameMetaData = rootFrame.Metadata.GetTiffMetadata();
                Assert.NotNull(frameMetaData);

                ImageMetadata imageMetaData = image.Metadata;
                Assert.NotNull(imageMetaData);
                Assert.Equal(PixelResolutionUnit.PixelsPerInch, imageMetaData.ResolutionUnits);
                Assert.Equal(10, imageMetaData.HorizontalResolution);
                Assert.Equal(10, imageMetaData.VerticalResolution);

                TiffMetadata tiffMetaData = image.Metadata.GetTiffMetadata();
                Assert.NotNull(tiffMetaData);
                Assert.Equal(ByteOrder.LittleEndian, tiffMetaData.ByteOrder);
                Assert.Equal(TiffBitsPerPixel.Bit4, tiffMetaData.BitsPerPixel);

                VerifyExpectedFrameMetaDataIsPresent(frameMetaData);
            }
        }

        private static void VerifyExpectedFrameMetaDataIsPresent(TiffFrameMetadata frameMetaData)
        {
            Assert.Equal(30, frameMetaData.ExifProfile.Values.Count);
            Assert.Equal(TiffBitsPerSample.Bit4, frameMetaData.BitsPerSample);
            Assert.Equal(TiffCompression.Lzw, frameMetaData.Compression);
            Assert.Equal(TiffPhotometricInterpretation.PaletteColor, frameMetaData.PhotometricInterpretation);
            Assert.Equal("This is Название", frameMetaData.ExifProfile.GetValue(ExifTag.ImageDescription).Value);
            Assert.Equal("This is Изготовитель камеры", frameMetaData.ExifProfile.GetValue(ExifTag.Make).Value);
            Assert.Equal("This is Модель камеры", frameMetaData.ExifProfile.GetValue(ExifTag.Model).Value);
            Assert.Equal(new Number[] {8u}, frameMetaData.StripOffsets, new NumberComparer());
            Assert.Equal(1, frameMetaData.SamplesPerPixel.GetValueOrDefault());
            Assert.Equal(32u, frameMetaData.RowsPerStrip);
            Assert.Equal(new Number[] {297u}, frameMetaData.StripByteCounts, new NumberComparer());
            Assert.Equal(PixelResolutionUnit.PixelsPerInch, frameMetaData.ResolutionUnit);
            Assert.Equal(10, frameMetaData.HorizontalResolution);
            Assert.Equal(10, frameMetaData.VerticalResolution);
            Assert.Equal(TiffPlanarConfiguration.Chunky, frameMetaData.PlanarConfiguration);
            Assert.Equal("IrfanView", frameMetaData.ExifProfile.GetValue(ExifTag.Software).Value);
            Assert.Null(frameMetaData.ExifProfile.GetValue(ExifTag.DateTime)?.Value);
            Assert.Equal("This is author1;Author2", frameMetaData.ExifProfile.GetValue(ExifTag.Artist).Value);
            Assert.Null(frameMetaData.ExifProfile.GetValue(ExifTag.HostComputer)?.Value);
            Assert.Equal(48, frameMetaData.ColorMap.Length);
            Assert.Equal(10537, frameMetaData.ColorMap[0]);
            Assert.Equal(14392, frameMetaData.ColorMap[1]);
            Assert.Equal(58596, frameMetaData.ColorMap[46]);
            Assert.Equal(3855, frameMetaData.ColorMap[47]);

            Assert.Null(frameMetaData.ExtraSamples);
            Assert.Equal(TiffPredictor.None, frameMetaData.Predictor);
            Assert.Null(frameMetaData.SampleFormat);
            Assert.Equal("This is Авторские права", frameMetaData.ExifProfile.GetValue(ExifTag.Copyright).Value);
            Assert.Equal(4, frameMetaData.ExifProfile.GetValue(ExifTag.Rating).Value);
            Assert.Equal(75, frameMetaData.ExifProfile.GetValue(ExifTag.RatingPercent).Value);
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
                Assert.Null(frame0MetaData.OldSubfileType);
                Assert.Equal(255, image.Frames[0].Width);
                Assert.Equal(255, image.Frames[0].Height);

                TiffFrameMetadata frame1MetaData = image.Frames[1].Metadata.GetTiffMetadata();
                Assert.Equal(TiffNewSubfileType.Preview, frame1MetaData.SubfileType);
                Assert.Null(frame1MetaData.OldSubfileType);
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
            ImageFrame<TPixel> frameRootInput = image.Frames.RootFrame;

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
            TiffMetadata tiffMetaDataEncodedImage = encodedImage.Metadata.GetTiffMetadata();
            TiffFrameMetadata tiffMetaDataEncodedRootFrame = encodedImage.Frames.RootFrame.Metadata.GetTiffMetadata();
            ImageFrame<Rgba32> rootFrameEncodedImage = encodedImage.Frames.RootFrame;

            Assert.Equal(TiffBitsPerPixel.Bit24, tiffMetaDataEncodedImage.BitsPerPixel);
            Assert.Equal(TiffCompression.None, tiffMetaDataEncodedRootFrame.Compression);

            Assert.Equal(inputMetaData.HorizontalResolution, encodedImageMetaData.HorizontalResolution);
            Assert.Equal(inputMetaData.VerticalResolution, encodedImageMetaData.VerticalResolution);
            Assert.Equal(inputMetaData.ResolutionUnits, encodedImageMetaData.ResolutionUnits);

            Assert.Equal(frameRootInput.Width, rootFrameEncodedImage.Width);
            Assert.Equal(frameRootInput.Height, rootFrameEncodedImage.Height);
            Assert.Equal(frameMetaInput.ResolutionUnit, tiffMetaDataEncodedRootFrame.ResolutionUnit);
            Assert.Equal(frameMetaInput.HorizontalResolution, tiffMetaDataEncodedRootFrame.HorizontalResolution);
            Assert.Equal(frameMetaInput.VerticalResolution, tiffMetaDataEncodedRootFrame.VerticalResolution);

            Assert.Equal(tiffMetaInput.XmpProfile, tiffMetaDataEncodedImage.XmpProfile);

            Assert.Equal("IrfanView", frameMetaInput.ExifProfile.GetValue(ExifTag.Software).Value);
            Assert.Equal("This is Название", frameMetaInput.ExifProfile.GetValue(ExifTag.ImageDescription).Value);
            Assert.Equal("This is Изготовитель камеры", frameMetaInput.ExifProfile.GetValue(ExifTag.Make).Value);
            Assert.Equal("This is Авторские права", frameMetaInput.ExifProfile.GetValue(ExifTag.Copyright).Value);

            Assert.Equal(frameMetaInput.ExifProfile.GetValue(ExifTag.ImageDescription).Value, tiffMetaDataEncodedRootFrame.ExifProfile.GetValue(ExifTag.ImageDescription).Value);
            Assert.Equal(frameMetaInput.ExifProfile.GetValue(ExifTag.Make).Value, tiffMetaDataEncodedRootFrame.ExifProfile.GetValue(ExifTag.Make).Value);
            Assert.Equal(frameMetaInput.ExifProfile.GetValue(ExifTag.Copyright).Value, tiffMetaDataEncodedRootFrame.ExifProfile.GetValue(ExifTag.Copyright).Value);
        }
    }
}
