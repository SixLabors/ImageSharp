// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Category", "Tiff.BlackBox")]
    [Trait("Category", "Tiff")]
    public class TiffMetadataTests
    {
        public static readonly string[] MetadataImages = TestImages.Tiff.Metadata;

        [Theory]
        [WithFile(TestImages.Tiff.SampleMetadata, PixelTypes.Rgba32, false)]
        [WithFile(TestImages.Tiff.SampleMetadata, PixelTypes.Rgba32, true)]
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
                Assert.Equal(TiffByteOrder.LittleEndian, meta.ByteOrder);
                Assert.Equal(PixelResolutionUnit.PixelsPerInch, image.Metadata.ResolutionUnits);
                Assert.Equal(10, image.Metadata.HorizontalResolution);
                Assert.Equal(10, image.Metadata.VerticalResolution);

                TiffFrameMetadata frame = image.Frames.RootFrame.Metadata.GetTiffMetadata();
                Assert.Equal(32u, frame.Width);
                Assert.Equal(32u, frame.Height);
                Assert.Equal(new ushort[] { 8, 8, 8 }, frame.BitsPerSample);
                Assert.Equal(TiffCompression.Lzw, frame.Compression);
                Assert.Equal(TiffPhotometricInterpretation.Rgb, frame.PhotometricInterpretation);
                Assert.Equal("This is Название", frame.ImageDescription);
                Assert.Equal("This is Изготовитель камеры", frame.Make);
                Assert.Equal("This is Модель камеры", frame.Model);
                Assert.Equal(new uint[] { 8 }, frame.StripOffsets);
                Assert.Equal(32u, frame.RowsPerStrip);
                Assert.Equal(new uint[] { 750 }, frame.StripByteCounts);
                Assert.Equal(10, frame.HorizontalResolution);
                Assert.Equal(10, frame.VerticalResolution);
                Assert.Equal(TiffPlanarConfiguration.Chunky, frame.PlanarConfiguration);
                Assert.Equal(TiffResolutionUnit.Inch, frame.ResolutionUnit);
                Assert.Equal("IrfanView", frame.Software);
                Assert.Equal(null, frame.DateTime);
                Assert.Equal("This is;Автор", frame.Artist);
                Assert.Equal(null, frame.HostComputer);
                Assert.Equal(null, frame.ColorMap);
                Assert.Equal(null, frame.ExtraSamples);
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
                Assert.Equal(null, frame0.SubfileType);
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
