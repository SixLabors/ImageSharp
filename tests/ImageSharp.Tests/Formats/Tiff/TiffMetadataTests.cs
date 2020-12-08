// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats.Experimental.Tiff;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    public class TiffMetadataTests
    {
        public TiffMetadataTests()
        {
            Configuration.Default.ImageFormatsManager.AddImageFormat(TiffFormat.Instance);
            Configuration.Default.ImageFormatsManager.AddImageFormatDetector(new TiffImageFormatDetector());
            Configuration.Default.ImageFormatsManager.SetDecoder(TiffFormat.Instance, new TiffDecoder());
            Configuration.Default.ImageFormatsManager.SetEncoder(TiffFormat.Instance, new TiffEncoder());
        }

        [Fact]
        public void CloneIsDeep()
        {
            var meta = new TiffMetadata
            {
                Compression = TiffCompression.Deflate,
                BitsPerPixel = TiffBitsPerPixel.Pixel8,
                ByteOrder = ByteOrder.BigEndian,
                XmpProfile = new byte[3]
            };

            var clone = (TiffMetadata)meta.DeepClone();

            clone.Compression = TiffCompression.None;
            clone.BitsPerPixel = TiffBitsPerPixel.Pixel24;
            clone.ByteOrder = ByteOrder.LittleEndian;
            clone.XmpProfile = new byte[1];

            Assert.False(meta.Compression == clone.Compression);
            Assert.False(meta.BitsPerPixel == clone.BitsPerPixel);
            Assert.False(meta.ByteOrder == clone.ByteOrder);
            Assert.False(meta.XmpProfile.SequenceEqual(clone.XmpProfile));
        }

        [Theory]
        [InlineData(Calliphora_BiColorUncompressed, TiffBitsPerPixel.Pixel1)]
        [InlineData(GrayscaleUncompressed, TiffBitsPerPixel.Pixel8)]
        [InlineData(RgbUncompressed, TiffBitsPerPixel.Pixel24)]
        public void Identify_DetectsCorrectBitPerPixel(string imagePath, TiffBitsPerPixel expectedBitsPerPixel)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            IImageInfo imageInfo = Image.Identify(stream);

            Assert.NotNull(imageInfo);
            TiffMetadata tiffMetadata = imageInfo.Metadata.GetTiffMetadata();
            Assert.NotNull(tiffMetadata);
            Assert.Equal(expectedBitsPerPixel, tiffMetadata.BitsPerPixel);
        }

        [Theory]
        [InlineData(GrayscaleUncompressed, TiffCompression.None)]
        [InlineData(RgbDeflate, TiffCompression.Deflate)]
        [InlineData(SmallRgbLzw, TiffCompression.Lzw)]
        [InlineData(Calliphora_Fax3Compressed, TiffCompression.CcittGroup3Fax)]
        [InlineData(Calliphora_Fax4Compressed, TiffCompression.CcittGroup4Fax)]
        [InlineData(Calliphora_HuffmanCompressed, TiffCompression.Ccitt1D)]
        [InlineData(Calliphora_RgbPackbits, TiffCompression.PackBits)]
        public void Identify_DetectsCorrectCompression(string imagePath, TiffCompression expectedCompression)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            IImageInfo imageInfo = Image.Identify(stream);

            Assert.NotNull(imageInfo);
            TiffMetadata tiffMetadata = imageInfo.Metadata.GetTiffMetadata();
            Assert.NotNull(tiffMetadata);
            Assert.Equal(expectedCompression, tiffMetadata.Compression);
        }

        [Theory]
        [InlineData(GrayscaleUncompressed, ByteOrder.BigEndian)]
        [InlineData(LittleEndianByteOrder, ByteOrder.LittleEndian)]
        public void Identify_DetectsCorrectByteOrder(string imagePath, ByteOrder expectedByteOrder)
        {
            var testFile = TestFile.Create(imagePath);
            using var stream = new MemoryStream(testFile.Bytes, false);

            IImageInfo imageInfo = Image.Identify(stream);

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
        [WithFile(TestImages.Tiff.SampleMetadata, PixelTypes.Rgba32)]
        public void BaselineTags<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TiffDecoder()))
            {
                TiffMetadata meta = image.Metadata.GetTiffMetadata();

                Assert.NotNull(meta);
                Assert.Equal(ByteOrder.LittleEndian, meta.ByteOrder);
                Assert.Equal(PixelResolutionUnit.PixelsPerInch, image.Metadata.ResolutionUnits);
                Assert.Equal(10, image.Metadata.HorizontalResolution);
                Assert.Equal(10, image.Metadata.VerticalResolution);

                TiffFrameMetadata frame = image.Frames.RootFrame.Metadata.GetTiffMetadata();
                Assert.Equal(32u, frame.Width);
                Assert.Equal(32u, frame.Height);
                Assert.Equal(new ushort[] { 4 }, frame.BitsPerSample);
                Assert.Equal(TiffCompression.Lzw, frame.Compression);
                Assert.Equal(TiffPhotometricInterpretation.PaletteColor, frame.PhotometricInterpretation);
                Assert.Equal("This is Название", frame.ImageDescription);
                Assert.Equal("This is Изготовитель камеры", frame.Make);
                Assert.Equal("This is Модель камеры", frame.Model);
                Assert.Equal(new uint[] { 8 }, frame.StripOffsets);
                Assert.Equal(1, frame.SamplesPerPixel);
                Assert.Equal(32u, frame.RowsPerStrip);
                Assert.Equal(new uint[] { 297 }, frame.StripByteCounts);
                Assert.Equal(10, frame.HorizontalResolution);
                Assert.Equal(10, frame.VerticalResolution);
                Assert.Equal(TiffPlanarConfiguration.Chunky, frame.PlanarConfiguration);
                Assert.Equal(TiffResolutionUnit.Inch, frame.ResolutionUnit);
                Assert.Equal("IrfanView", frame.Software);
                Assert.Null(frame.DateTime);
                Assert.Equal("This is author1;Author2", frame.Artist);
                Assert.Null(frame.HostComputer);
                Assert.Equal(48, frame.ColorMap.Length);
                Assert.Equal(10537, frame.ColorMap[0]);
                Assert.Equal(14392, frame.ColorMap[1]);
                Assert.Equal(58596, frame.ColorMap[46]);
                Assert.Equal(3855, frame.ColorMap[47]);

                Assert.Null(frame.ExtraSamples);
                Assert.Equal(TiffPredictor.None, frame.Predictor);
                Assert.Null(frame.SampleFormat);
                Assert.Equal("This is Авторские права", frame.Copyright);
            }
        }

        [Theory]
        [WithFile(TestImages.Tiff.MultiframeDeflateWithPreview, PixelTypes.Rgba32)]
        public void SubfileType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new TiffDecoder()))
            {
                TiffMetadata meta = image.Metadata.GetTiffMetadata();
                Assert.NotNull(meta);

                Assert.Equal(2, image.Frames.Count);

                TiffFrameMetadata frame0 = image.Frames[0].Metadata.GetTiffMetadata();
                Assert.Equal(TiffNewSubfileType.FullImage, frame0.NewSubfileType);
                Assert.Null(frame0.SubfileType);
                Assert.Equal(255u, frame0.Width);
                Assert.Equal(255u, frame0.Height);

                TiffFrameMetadata frame1 = image.Frames[1].Metadata.GetTiffMetadata();
                Assert.Equal(TiffNewSubfileType.Preview, frame1.NewSubfileType);
                Assert.Equal(TiffSubfileType.Preview, frame1.SubfileType);
                Assert.Equal(255u, frame1.Width);
                Assert.Equal(255u, frame1.Height);
            }
        }
    }
}
