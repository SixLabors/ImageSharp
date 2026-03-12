// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Exif;

[Trait("Profile", "Exif")]
public class ExifProfileTests
{
    public enum TestImageWriteFormat
    {
        /// <summary>
        /// Writes a jpg file.
        /// </summary>
        Jpeg,

        /// <summary>
        /// Writes a png file.
        /// </summary>
        Png,

        /// <summary>
        /// Writes a lossless webp file.
        /// </summary>
        WebpLossless,

        /// <summary>
        /// Writes a lossy webp file.
        /// </summary>
        WebpLossy
    }

    private static readonly Dictionary<ExifTag, object> TestProfileValues = new()
    {
        { ExifTag.Software, "Software" },
        { ExifTag.Copyright, "Copyright" },
        { ExifTag.Orientation, (ushort)5 },
        { ExifTag.ShutterSpeedValue, new SignedRational(75.55) },
        { ExifTag.ImageDescription, "ImageDescription" },
        { ExifTag.ExposureTime, new Rational(1.0 / 1600.0) },
        { ExifTag.Model, "Model" },
        { ExifTag.XPAuthor, "The XPAuthor text" },
        { ExifTag.UserComment, new EncodedString(EncodedString.CharacterCode.Unicode, "The Unicode text") },
        { ExifTag.GPSAreaInformation, new EncodedString("Default constructor text (GPSAreaInformation)") },
    };

    [Theory]
    [InlineData(TestImageWriteFormat.Jpeg)]
    [InlineData(TestImageWriteFormat.Png)]
    [InlineData(TestImageWriteFormat.WebpLossless)]
    [InlineData(TestImageWriteFormat.WebpLossy)]
    public void Constructor(TestImageWriteFormat imageFormat)
    {
        Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Calliphora).CreateRgba32Image();

        Assert.Null(image.Metadata.ExifProfile);

        const string expected = "Dirk Lemstra";
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Copyright, expected);

        image = WriteAndRead(image, imageFormat);

        Assert.NotNull(image.Metadata.ExifProfile);
        Assert.Equal(1, image.Metadata.ExifProfile.Values.Count);

        IExifValue<string> value = image.Metadata.ExifProfile.GetValue(ExifTag.Copyright);

        Assert.NotNull(value);
        Assert.Equal(expected, value.Value);
        image.Dispose();
    }

    [Fact]
    public void ConstructorEmpty()
    {
        new ExifProfile(null);
        new ExifProfile([]);
    }

    [Fact]
    public void EmptyWriter()
    {
        ExifProfile profile = new() { Parts = ExifParts.GpsTags };
        profile.SetValue(ExifTag.Copyright, "Copyright text");

        byte[] bytes = profile.ToByteArray();

        Assert.NotNull(bytes);
        Assert.Empty(bytes);
    }

    [Fact]
    public void ConstructorCopy()
    {
        Assert.Throws<NullReferenceException>(() => ((ExifProfile)null).DeepClone());

        ExifProfile profile = GetExifProfile();

        ExifProfile clone = profile.DeepClone();
        TestProfile(clone);

        profile.SetValue(ExifTag.ColorSpace, (ushort)2);

        clone = profile.DeepClone();
        TestProfile(clone);
    }

    [Theory]
    [InlineData(TestImageWriteFormat.Jpeg)]
    [InlineData(TestImageWriteFormat.Png)]
    [InlineData(TestImageWriteFormat.WebpLossless)]
    [InlineData(TestImageWriteFormat.WebpLossy)]
    public void WriteFraction(TestImageWriteFormat imageFormat)
    {
        using MemoryStream memStream = new();
        double exposureTime = 1.0 / 1600;

        ExifProfile profile = GetExifProfile();

        profile.SetValue(ExifTag.ExposureTime, new Rational(exposureTime));

        Image<Rgba32> image = new(1, 1);
        image.Metadata.ExifProfile = profile;

        image = WriteAndRead(image, imageFormat);

        profile = image.Metadata.ExifProfile;
        Assert.NotNull(profile);

        IExifValue<Rational> value = profile.GetValue(ExifTag.ExposureTime);
        Assert.NotNull(value);
        Assert.NotEqual(exposureTime, value.Value.ToDouble());

        memStream.Position = 0;
        profile = GetExifProfile();

        profile.SetValue(ExifTag.ExposureTime, new Rational(exposureTime, true));
        image.Metadata.ExifProfile = profile;

        image = WriteAndRead(image, imageFormat);

        profile = image.Metadata.ExifProfile;
        Assert.NotNull(profile);

        value = profile.GetValue(ExifTag.ExposureTime);
        Assert.Equal(exposureTime, value.Value.ToDouble());

        image.Dispose();
    }

    [Theory]
    [InlineData(TestImageWriteFormat.Jpeg)]
    [InlineData(TestImageWriteFormat.Png)]
    [InlineData(TestImageWriteFormat.WebpLossless)]
    [InlineData(TestImageWriteFormat.WebpLossy)]
    public void ReadWriteInfinity(TestImageWriteFormat imageFormat)
    {
        Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image();
        image.Metadata.ExifProfile.SetValue(ExifTag.ExposureBiasValue, new SignedRational(double.PositiveInfinity));

        image = WriteAndReadJpeg(image);
        IExifValue<SignedRational> value = image.Metadata.ExifProfile.GetValue(ExifTag.ExposureBiasValue);
        Assert.NotNull(value);
        Assert.Equal(new SignedRational(double.PositiveInfinity), value.Value);

        image.Metadata.ExifProfile.SetValue(ExifTag.ExposureBiasValue, new SignedRational(double.NegativeInfinity));

        image = WriteAndRead(image, imageFormat);
        value = image.Metadata.ExifProfile.GetValue(ExifTag.ExposureBiasValue);
        Assert.NotNull(value);
        Assert.Equal(new SignedRational(double.NegativeInfinity), value.Value);

        image.Metadata.ExifProfile.SetValue(ExifTag.FlashEnergy, new Rational(double.NegativeInfinity));

        image = WriteAndRead(image, imageFormat);
        IExifValue<Rational> value2 = image.Metadata.ExifProfile.GetValue(ExifTag.FlashEnergy);
        Assert.NotNull(value2);
        Assert.Equal(new Rational(double.PositiveInfinity), value2.Value);

        image.Dispose();
    }

    [Theory]
    /* The original exif profile has 19 values, the written profile should be 3 less.
     1 x due to setting of null "ReferenceBlackWhite" value.
     2 x due to use of non-standard padding tag 0xEA1C listed in EXIF Tool. We can read those values but adhere
     strictly to the 2.3.1 specification when writing. (TODO: Support 2.3.2)
     https://exiftool.org/TagNames/EXIF.html */
    [InlineData(TestImageWriteFormat.Jpeg, 18)]
    [InlineData(TestImageWriteFormat.Png, 18)]
    [InlineData(TestImageWriteFormat.WebpLossless, 18)]
    public void SetValue(TestImageWriteFormat imageFormat, int expectedProfileValueCount)
    {
        Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image();
        image.Metadata.ExifProfile.SetValue(ExifTag.Software, "ImageSharp");

        IExifValue<string> software = image.Metadata.ExifProfile.GetValue(ExifTag.Software);
        Assert.Equal("ImageSharp", software.Value);

        // ExifString can set integer values.
        Assert.True(software.TrySetValue(15));
        Assert.False(software.TrySetValue(15F));

        image.Metadata.ExifProfile.SetValue(ExifTag.ShutterSpeedValue, new SignedRational(75.55));

        IExifValue<SignedRational> shutterSpeed = image.Metadata.ExifProfile.GetValue(ExifTag.ShutterSpeedValue);

        Assert.Equal(new SignedRational(7555, 100), shutterSpeed.Value);
        Assert.False(shutterSpeed.TrySetValue(75));

        image.Metadata.ExifProfile.SetValue(ExifTag.XResolution, new Rational(150.0));

        // We also need to change this value because this overrides XResolution when the image is written.
        image.Metadata.HorizontalResolution = 150.0;

        IExifValue<Rational> xResolution = image.Metadata.ExifProfile.GetValue(ExifTag.XResolution);
        Assert.Equal(new Rational(150, 1), xResolution.Value);

        Assert.False(xResolution.TrySetValue("ImageSharp"));

        image.Metadata.ExifProfile.SetValue(ExifTag.ReferenceBlackWhite, null);

        IExifValue<Rational[]> referenceBlackWhite = image.Metadata.ExifProfile.GetValue(ExifTag.ReferenceBlackWhite);
        Assert.Null(referenceBlackWhite.Value);

        Rational[] expectedLatitude = [new(12.3), new(4.56), new(789.0)];
        image.Metadata.ExifProfile.SetValue(ExifTag.GPSLatitude, expectedLatitude);

        IExifValue<Rational[]> latitude = image.Metadata.ExifProfile.GetValue(ExifTag.GPSLatitude);
        Assert.Equal(expectedLatitude, latitude.Value);

        // todo: duplicate tags
        Assert.Equal(2, image.Metadata.ExifProfile.Values.Count(v => (ushort)v.Tag == 59932));

        image = WriteAndRead(image, imageFormat);

        Assert.NotNull(image.Metadata.ExifProfile);
        Assert.Equal(0, image.Metadata.ExifProfile.Values.Count(v => (ushort)v.Tag == 59932));

        Assert.Equal(expectedProfileValueCount, image.Metadata.ExifProfile.Values.Count);

        software = image.Metadata.ExifProfile.GetValue(ExifTag.Software);
        Assert.Equal("15", software.Value);

        shutterSpeed = image.Metadata.ExifProfile.GetValue(ExifTag.ShutterSpeedValue);
        Assert.Equal(new SignedRational(75.55), shutterSpeed.Value);

        xResolution = image.Metadata.ExifProfile.GetValue(ExifTag.XResolution);
        Assert.Equal(new Rational(150.0), xResolution.Value);

        referenceBlackWhite = image.Metadata.ExifProfile.GetValue(ExifTag.ReferenceBlackWhite, false);
        Assert.Null(referenceBlackWhite);

        latitude = image.Metadata.ExifProfile.GetValue(ExifTag.GPSLatitude);
        Assert.Equal(expectedLatitude, latitude.Value);

        image.Dispose();
    }

    [Theory]
    [InlineData(TestImageWriteFormat.Jpeg)]
    [InlineData(TestImageWriteFormat.Png)]
    [InlineData(TestImageWriteFormat.WebpLossless)]
    [InlineData(TestImageWriteFormat.WebpLossy)]
    public void WriteOnlyExifTags_Works(TestImageWriteFormat imageFormat)
    {
        // Arrange
        Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image();
        image.Metadata.ExifProfile.Parts = ExifParts.ExifTags;

        // Act
        image = WriteAndRead(image, imageFormat);

        // Assert
        Assert.NotNull(image.Metadata.ExifProfile);
        Assert.Equal(7, image.Metadata.ExifProfile.Values.Count);
        foreach (IExifValue exifProfileValue in image.Metadata.ExifProfile.Values)
        {
            Assert.True(ExifTags.GetPart(exifProfileValue.Tag) == ExifParts.ExifTags);
        }

        image.Dispose();
    }

    [Fact]
    public void RemoveEntry_Works()
    {
        // Arrange
        using Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image();
        int profileCount = image.Metadata.ExifProfile.Values.Count;

        // Assert
        Assert.NotNull(image.Metadata.ExifProfile.GetValue(ExifTag.ColorSpace));
        Assert.True(image.Metadata.ExifProfile.RemoveValue(ExifTag.ColorSpace));
        Assert.False(image.Metadata.ExifProfile.RemoveValue(ExifTag.ColorSpace));
        Assert.Null(image.Metadata.ExifProfile.GetValue(ExifTag.ColorSpace, false));
        Assert.Equal(profileCount - 1, image.Metadata.ExifProfile.Values.Count);
    }

    [Fact]
    public void Syncs()
    {
        ExifProfile exifProfile = new();
        exifProfile.SetValue(ExifTag.XResolution, new Rational(200));
        exifProfile.SetValue(ExifTag.YResolution, new Rational(300));

        ImageMetadata metaData = new()
        {
            ExifProfile = exifProfile,
            HorizontalResolution = 200,
            VerticalResolution = 300
        };

        metaData.HorizontalResolution = 100;

        Assert.Equal(200, metaData.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
        Assert.Equal(300, metaData.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());

        exifProfile.Sync(metaData);

        Assert.Equal(100, metaData.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
        Assert.Equal(300, metaData.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());

        metaData.VerticalResolution = 150;

        Assert.Equal(100, metaData.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
        Assert.Equal(300, metaData.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());

        exifProfile.Sync(metaData);

        Assert.Equal(100, metaData.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
        Assert.Equal(150, metaData.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());
    }

    [Fact]
    public void Values()
    {
        ExifProfile profile = GetExifProfile();

        TestProfile(profile);
        bool retVal = profile.TryCreateThumbnail(out Image thumbnail);
        Assert.True(retVal);
        Assert.NotNull(thumbnail);
        Assert.Equal(256, thumbnail.Width);
        Assert.Equal(170, thumbnail.Height);

        retVal = profile.TryCreateThumbnail<Rgba32>(out Image<Rgba32> genericThumbnail);
        Assert.True(retVal);
        Assert.NotNull(genericThumbnail);
        Assert.Equal(256, genericThumbnail.Width);
        Assert.Equal(170, genericThumbnail.Height);
    }

    [Fact]
    public void ReadWriteLargeProfileJpg()
    {
        ExifTag<string>[] tags = [ExifTag.Software, ExifTag.Copyright, ExifTag.Model, ExifTag.ImageDescription];
        foreach (ExifTag<string> tag in tags)
        {
            // Arrange
            StringBuilder junk = new();
            for (int i = 0; i < 65600; i++)
            {
                junk.Append('a');
            }

            Image<Rgba32> image = new(100, 100);
            ExifProfile expectedProfile = CreateExifProfile();
            List<ExifTag> expectedProfileTags = expectedProfile.Values.Select(x => x.Tag).ToList();
            expectedProfile.SetValue(tag, junk.ToString());
            image.Metadata.ExifProfile = expectedProfile;

            // Act
            Image<Rgba32> reloadedImage = WriteAndRead(image, TestImageWriteFormat.Jpeg);

            // Assert
            ExifProfile actualProfile = reloadedImage.Metadata.ExifProfile;
            Assert.NotNull(actualProfile);

            foreach (ExifTag expectedProfileTag in expectedProfileTags)
            {
                IExifValue actualProfileValue = actualProfile.GetValueInternal(expectedProfileTag);
                IExifValue expectedProfileValue = expectedProfile.GetValueInternal(expectedProfileTag);
                Assert.Equal(expectedProfileValue.GetValue(), actualProfileValue.GetValue());
            }

            IExifValue<string> expected = expectedProfile.GetValue(tag);
            IExifValue<string> actual = actualProfile.GetValue(tag);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void ExifTypeUndefined()
    {
        // This image contains an 802 byte EXIF profile.
        // It has a tag with an index offset of 18,481,152 bytes (overrunning the data)
        using Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Progressive.Bad.ExifUndefType).CreateRgba32Image();
        Assert.NotNull(image);

        ExifProfile profile = image.Metadata.ExifProfile;
        Assert.NotNull(profile);

        foreach (ExifValue value in profile.Values)
        {
            if (value.DataType == ExifDataType.Undefined)
            {
                Assert.True(value.IsArray);
                Assert.Equal(4U, 4 * ExifDataTypes.GetSize(value.DataType));
            }
        }
    }

    [Fact]
    public void TestArrayValueWithUnspecifiedSize()
    {
        // This images contains array in the exif profile that has zero components.
        using Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Issues.InvalidCast520).CreateRgba32Image();

        ExifProfile profile = image.Metadata.ExifProfile;
        Assert.NotNull(profile);

        // Force parsing of the profile.
        Assert.Equal(25, profile.Values.Count);

        // todo: duplicate tags (from root container and subIfd)
        Assert.Equal(2, profile.Values.Count(v => (ExifTagValue)(ushort)v.Tag == ExifTagValue.DateTime));

        byte[] bytes = profile.ToByteArray();
        Assert.Equal(531, bytes.Length);

        ExifProfile profile2 = new(bytes);
        Assert.Equal(25, profile2.Values.Count);
    }

    [Theory]
    [InlineData(TestImageWriteFormat.Jpeg)]
    [InlineData(TestImageWriteFormat.Png)]
    [InlineData(TestImageWriteFormat.WebpLossless)]
    [InlineData(TestImageWriteFormat.WebpLossy)]
    public void WritingImagePreservesExifProfile(TestImageWriteFormat imageFormat)
    {
        // Arrange
        Image<Rgba32> image = new(1, 1);
        image.Metadata.ExifProfile = CreateExifProfile();

        // Act
        using Image<Rgba32> reloadedImage = WriteAndRead(image, imageFormat);

        // Assert
        ExifProfile actual = reloadedImage.Metadata.ExifProfile;
        Assert.NotNull(actual);
        foreach (KeyValuePair<ExifTag, object> expectedProfileValue in TestProfileValues)
        {
            IExifValue actualProfileValue = actual.GetValueInternal(expectedProfileValue.Key);
            Assert.NotNull(actualProfileValue);
            Assert.Equal(expectedProfileValue.Value, actualProfileValue.GetValue());
        }
    }

    [Fact]
    public void ProfileToByteArray()
    {
        // Arrange
        byte[] exifBytesWithoutExifCode = ExifConstants.LittleEndianByteOrderMarker.ToArray();
        ExifProfile expectedProfile = CreateExifProfile();
        List<ExifTag> expectedProfileTags = expectedProfile.Values.Select(x => x.Tag).ToList();

        // Act
        byte[] actualBytes = expectedProfile.ToByteArray();
        ExifProfile actualProfile = new(actualBytes);

        // Assert
        Assert.NotNull(actualBytes);
        Assert.NotEmpty(actualBytes);
        Assert.Equal(exifBytesWithoutExifCode, actualBytes.Take(exifBytesWithoutExifCode.Length).ToArray());
        foreach (ExifTag expectedProfileTag in expectedProfileTags)
        {
            IExifValue actualProfileValue = actualProfile.GetValueInternal(expectedProfileTag);
            IExifValue expectedProfileValue = expectedProfile.GetValueInternal(expectedProfileTag);
            Assert.Equal(expectedProfileValue.GetValue(), actualProfileValue.GetValue());
        }
    }

    private static ExifProfile CreateExifProfile()
    {
        ExifProfile profile = new();

        foreach (KeyValuePair<ExifTag, object> exifProfileValue in TestProfileValues)
        {
            profile.SetValueInternal(exifProfileValue.Key, exifProfileValue.Value);
        }

        return profile;
    }

    [Fact]
    public void IfdStructure()
    {
        ExifProfile exif = new();
        exif.SetValue(ExifTag.XPAuthor, "Dan Petitt");

        Span<byte> actualBytes = exif.ToByteArray();

        // Assert
        int ifdOffset = ExifConstants.LittleEndianByteOrderMarker.Length;
        Assert.Equal(8U, BinaryPrimitives.ReadUInt32LittleEndian(actualBytes.Slice(ifdOffset, 4)));

        int nextIfdPointerOffset = ExifConstants.LittleEndianByteOrderMarker.Length + 4 + 2 + 12;
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(actualBytes.Slice(nextIfdPointerOffset, 4)));
    }

    internal static ExifProfile GetExifProfile()
    {
        using Image<Rgba32> image = TestFile.Create(TestImages.Jpeg.Baseline.Floorplan).CreateRgba32Image();

        ExifProfile profile = image.Metadata.ExifProfile;
        Assert.NotNull(profile);

        return profile;
    }

    private static Image<Rgba32> WriteAndRead(Image<Rgba32> image, TestImageWriteFormat imageFormat)
    {
        switch (imageFormat)
        {
            case TestImageWriteFormat.Jpeg:
                return WriteAndReadJpeg(image);
            case TestImageWriteFormat.Png:
                return WriteAndReadPng(image);
            case TestImageWriteFormat.WebpLossless:
                return WriteAndReadWebp(image, WebpFileFormatType.Lossless);
            case TestImageWriteFormat.WebpLossy:
                return WriteAndReadWebp(image, WebpFileFormatType.Lossy);
            default:
                throw new ArgumentException("Unexpected test image format, only Jpeg and Png are allowed");
        }
    }

    private static Image<Rgba32> WriteAndReadJpeg(Image<Rgba32> image)
    {
        using MemoryStream memStream = new();
        image.SaveAsJpeg(memStream);
        image.Dispose();

        memStream.Position = 0;
        return Image.Load<Rgba32>(memStream);
    }

    private static Image<Rgba32> WriteAndReadPng(Image<Rgba32> image)
    {
        using MemoryStream memStream = new();
        image.SaveAsPng(memStream);
        image.Dispose();

        memStream.Position = 0;
        return Image.Load<Rgba32>(memStream);
    }

    private static Image<Rgba32> WriteAndReadWebp(Image<Rgba32> image, WebpFileFormatType fileFormat)
    {
        using MemoryStream memStream = new();
        image.SaveAsWebp(memStream, new WebpEncoder { FileFormat = fileFormat });
        image.Dispose();

        memStream.Position = 0;
        return Image.Load<Rgba32>(memStream);
    }

    private static void TestProfile(ExifProfile profile)
    {
        Assert.NotNull(profile);

        // todo: duplicate tags
        Assert.Equal(2, profile.Values.Count(v => (ushort)v.Tag == 59932));

        Assert.Equal(18, profile.Values.Count);

        foreach (IExifValue value in profile.Values)
        {
            Assert.NotNull(value.GetValue());
        }

        IExifValue<string> software = profile.GetValue(ExifTag.Software);
        Assert.Equal("Windows Photo Editor 10.0.10011.16384", software.Value);

        IExifValue<Rational> xResolution = profile.GetValue(ExifTag.XResolution);
        Assert.Equal(new Rational(300.0), xResolution.Value);

        IExifValue<Number> xDimension = profile.GetValue(ExifTag.PixelXDimension);
        Assert.Equal(2338U, xDimension.Value);
    }
}
