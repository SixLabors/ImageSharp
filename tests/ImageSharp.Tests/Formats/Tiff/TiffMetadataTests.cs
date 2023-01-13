// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public class TiffMetadataTests
{
    private class NumberComparer : IEqualityComparer<Number>
    {
        public bool Equals(Number x, Number y) => x.Equals(y);

        public int GetHashCode(Number obj) => obj.GetHashCode();
    }

    [Fact]
    public void TiffMetadata_CloneIsDeep()
    {
        TiffMetadata meta = new()
        {
            ByteOrder = ByteOrder.BigEndian,
        };

        TiffMetadata clone = (TiffMetadata)meta.DeepClone();

        clone.ByteOrder = ByteOrder.LittleEndian;

        Assert.False(meta.ByteOrder == clone.ByteOrder);
    }

    [Theory]
    [WithFile(SampleMetadata, PixelTypes.Rgba32)]
    public void TiffFrameMetadata_CloneIsDeep<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        TiffFrameMetadata meta = image.Frames.RootFrame.Metadata.GetTiffMetadata();
        TiffFrameMetadata cloneSameAsSampleMetaData = (TiffFrameMetadata)meta.DeepClone();
        VerifyExpectedTiffFrameMetaDataIsPresent(cloneSameAsSampleMetaData);

        TiffFrameMetadata clone = (TiffFrameMetadata)meta.DeepClone();

        clone.BitsPerPixel = TiffBitsPerPixel.Bit8;
        clone.Compression = TiffCompression.None;
        clone.PhotometricInterpretation = TiffPhotometricInterpretation.CieLab;
        clone.Predictor = TiffPredictor.Horizontal;

        Assert.False(meta.BitsPerPixel == clone.BitsPerPixel);
        Assert.False(meta.Compression == clone.Compression);
        Assert.False(meta.PhotometricInterpretation == clone.PhotometricInterpretation);
        Assert.False(meta.Predictor == clone.Predictor);
    }

    private static void VerifyExpectedTiffFrameMetaDataIsPresent(TiffFrameMetadata frameMetaData)
    {
        Assert.NotNull(frameMetaData);
        Assert.NotNull(frameMetaData.BitsPerPixel);
        Assert.Equal(TiffBitsPerPixel.Bit4, frameMetaData.BitsPerPixel);
        Assert.Equal(TiffCompression.Lzw, frameMetaData.Compression);
        Assert.Equal(TiffPhotometricInterpretation.PaletteColor, frameMetaData.PhotometricInterpretation);
    }

    [Theory]
    [InlineData(Calliphora_BiColorUncompressed, 1)]
    [InlineData(GrayscaleUncompressed, 8)]
    [InlineData(RgbUncompressed, 24)]
    public void Identify_DetectsCorrectBitPerPixel(string imagePath, int expectedBitsPerPixel)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        Image.TryIdentify(stream, out ImageInfo imageInfo);

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
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        Image.TryIdentify(stream, out ImageInfo imageInfo);

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
        DecoderOptions options = new() { SkipMetadata = ignoreMetadata };
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, options);
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
            Assert.Equal(2599, rootFrameMetaData.XmpProfile.Data.Length);
            Assert.Equal(25, rootFrameMetaData.ExifProfile.Values.Count);
        }
    }

    [Theory]
    [WithFile(InvalidIptcData, PixelTypes.Rgba32)]
    public void CanDecodeImage_WithIptcDataAsLong<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);

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
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        ImageFrame<TPixel> rootFrame = image.Frames.RootFrame;
        Assert.Equal(32, rootFrame.Width);
        Assert.Equal(32, rootFrame.Height);
        Assert.NotNull(rootFrame.Metadata.XmpProfile);
        Assert.Equal(2599, rootFrame.Metadata.XmpProfile.Data.Length);

        ExifProfile exifProfile = rootFrame.Metadata.ExifProfile;
        TiffFrameMetadata tiffFrameMetadata = rootFrame.Metadata.GetTiffMetadata();
        Assert.NotNull(exifProfile);

        Assert.Equal(25, exifProfile.Values.Count);
        Assert.Equal(TiffBitsPerPixel.Bit4, tiffFrameMetadata.BitsPerPixel);
        Assert.Equal(TiffCompression.Lzw, tiffFrameMetadata.Compression);
        Assert.Equal("ImageDescription", exifProfile.GetValue(ExifTag.ImageDescription).Value);
        Assert.Equal("Make", exifProfile.GetValue(ExifTag.Make).Value);
        Assert.Equal("Model", exifProfile.GetValue(ExifTag.Model).Value);
        Assert.Equal("ImageSharp", exifProfile.GetValue(ExifTag.Software).Value);
        Assert.Null(exifProfile.GetValue(ExifTag.DateTime)?.Value);
        Assert.Equal("Artist", exifProfile.GetValue(ExifTag.Artist).Value);
        Assert.Null(exifProfile.GetValue(ExifTag.HostComputer)?.Value);
        Assert.Equal("Copyright", exifProfile.GetValue(ExifTag.Copyright).Value);
        Assert.Equal(4, exifProfile.GetValue(ExifTag.Rating).Value);
        Assert.Equal(75, exifProfile.GetValue(ExifTag.RatingPercent).Value);
        Rational expectedResolution = new(10, 1, simplify: false);
        Assert.Equal(expectedResolution, exifProfile.GetValue(ExifTag.XResolution).Value);
        Assert.Equal(expectedResolution, exifProfile.GetValue(ExifTag.YResolution).Value);
        Assert.Equal(new Number[] { 8u }, exifProfile.GetValue(ExifTag.StripOffsets)?.Value, new NumberComparer());
        Assert.Equal(new Number[] { 285u }, exifProfile.GetValue(ExifTag.StripByteCounts)?.Value, new NumberComparer());
        Assert.Null(exifProfile.GetValue(ExifTag.ExtraSamples)?.Value);
        Assert.Equal(32u, exifProfile.GetValue(ExifTag.RowsPerStrip).Value);
        Assert.Null(exifProfile.GetValue(ExifTag.SampleFormat));
        Assert.Equal(PixelResolutionUnit.PixelsPerInch, UnitConverter.ExifProfileToResolutionUnit(exifProfile));
        ushort[] colorMap = exifProfile.GetValue(ExifTag.ColorMap)?.Value;
        Assert.NotNull(colorMap);
        Assert.Equal(48, colorMap.Length);
        Assert.Equal(4369, colorMap[0]);
        Assert.Equal(8738, colorMap[1]);
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
    }

    [Theory]
    [WithFile(MultiframeDeflateWithPreview, PixelTypes.Rgba32)]
    public void SubfileType<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
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

    [Theory]
    [WithFile(SampleMetadata, PixelTypes.Rgba32)]
    public void Encode_PreservesMetadata<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Load Tiff image
        DecoderOptions options = new() { SkipMetadata = false };
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, options);

        ImageMetadata inputMetaData = image.Metadata;
        ImageFrame<TPixel> rootFrameInput = image.Frames.RootFrame;
        TiffFrameMetadata frameMetaInput = rootFrameInput.Metadata.GetTiffMetadata();
        XmpProfile xmpProfileInput = rootFrameInput.Metadata.XmpProfile;
        ExifProfile exifProfileInput = rootFrameInput.Metadata.ExifProfile;

        Assert.Equal(TiffCompression.Lzw, frameMetaInput.Compression);
        Assert.Equal(TiffBitsPerPixel.Bit4, frameMetaInput.BitsPerPixel);

        // Save to Tiff
        TiffEncoder tiffEncoder = new() { PhotometricInterpretation = TiffPhotometricInterpretation.Rgb };
        using MemoryStream ms = new();
        image.Save(ms, tiffEncoder);

        // Assert
        ms.Position = 0;
        using Image<Rgba32> encodedImage = Image.Load<Rgba32>(ms);

        ImageMetadata encodedImageMetaData = encodedImage.Metadata;
        ImageFrame<Rgba32> rootFrameEncodedImage = encodedImage.Frames.RootFrame;
        TiffFrameMetadata tiffMetaDataEncodedRootFrame = rootFrameEncodedImage.Metadata.GetTiffMetadata();
        ExifProfile encodedImageExifProfile = rootFrameEncodedImage.Metadata.ExifProfile;
        XmpProfile encodedImageXmpProfile = rootFrameEncodedImage.Metadata.XmpProfile;

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
        Assert.Equal(exifProfileInput.GetValue(ExifTag.XResolution).Value.ToDouble(), encodedImageExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
        Assert.Equal(exifProfileInput.GetValue(ExifTag.YResolution).Value.ToDouble(), encodedImageExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());

        Assert.NotNull(xmpProfileInput);
        Assert.NotNull(encodedImageXmpProfile);
        Assert.Equal(xmpProfileInput.Data, encodedImageXmpProfile.Data);

        Assert.Equal(exifProfileInput.GetValue(ExifTag.Software).Value, encodedImageExifProfile.GetValue(ExifTag.Software).Value);
        Assert.Equal(exifProfileInput.GetValue(ExifTag.ImageDescription).Value, encodedImageExifProfile.GetValue(ExifTag.ImageDescription).Value);
        Assert.Equal(exifProfileInput.GetValue(ExifTag.Make).Value, encodedImageExifProfile.GetValue(ExifTag.Make).Value);
        Assert.Equal(exifProfileInput.GetValue(ExifTag.Copyright).Value, encodedImageExifProfile.GetValue(ExifTag.Copyright).Value);
        Assert.Equal(exifProfileInput.GetValue(ExifTag.Artist).Value, encodedImageExifProfile.GetValue(ExifTag.Artist).Value);
        Assert.Equal(exifProfileInput.GetValue(ExifTag.Orientation).Value, encodedImageExifProfile.GetValue(ExifTag.Orientation).Value);
        Assert.Equal(exifProfileInput.GetValue(ExifTag.Model).Value, encodedImageExifProfile.GetValue(ExifTag.Model).Value);

        Assert.Equal((ushort)TiffPlanarConfiguration.Chunky, encodedImageExifProfile.GetValue(ExifTag.PlanarConfiguration)?.Value);
        Assert.Equal(exifProfileInput.Values.Count, encodedImageExifProfile.Values.Count);
    }
}
