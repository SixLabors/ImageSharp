// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.IPTC;

public class IptcProfileTests
{
    public static IEnumerable<object[]> AllIptcTags()
    {
        foreach (object tag in Enum.GetValues(typeof(IptcTag)))
        {
            yield return [tag];
        }
    }

    [Fact]
    public void IptcProfile_WithUtf8Data_WritesEnvelopeRecord_Works()
    {
        // arrange
        IptcProfile profile = new();
        profile.SetValue(IptcTag.City, "ESPAÑA");
        profile.UpdateData();
        byte[] expectedEnvelopeData = [28, 1, 90, 0, 3, 27, 37, 71];

        // act
        byte[] profileBytes = profile.Data;

        // assert
        Assert.True(profileBytes.AsSpan(0, 8).SequenceEqual(expectedEnvelopeData));
    }

    /// <summary>
    /// Preserves the UTF-8 declaration when cloning creates a separate encoding instance.
    /// </summary>
    [Fact]
    public void IptcProfile_DeepClone_WithUtf8Data_WritesEnvelopeRecord_Works()
    {
        // arrange
        IptcProfile profile = new();
        profile.SetValue(IptcTag.City, "ESPAÑA");
        IptcProfile clone = profile.DeepClone();
        byte[] expectedEnvelopeData = [28, 1, 90, 0, 3, 27, 37, 71];

        // act
        clone.UpdateData();

        // assert
        Assert.True(clone.Data.AsSpan(0, 8).SequenceEqual(expectedEnvelopeData));
    }

    /// <summary>
    /// Declares UTF-8 for a caller-supplied encoding instance.
    /// </summary>
    [Fact]
    public void IptcProfile_WithUtf8EncodingInstance_WritesEnvelopeRecord_Works()
    {
        // arrange
        IptcProfile profile = new();
        profile.SetValue(IptcTag.City, new UTF8Encoding(false), "ESPAÑA");
        byte[] expectedEnvelopeData = [28, 1, 90, 0, 3, 27, 37, 71];

        // act
        profile.UpdateData();

        // assert
        Assert.True(profile.Data.AsSpan(0, 8).SequenceEqual(expectedEnvelopeData));
    }

    /// <summary>
    /// Keeps a cloned Latin-1 value in its original encoding without a UTF-8 declaration.
    /// </summary>
    [Fact]
    public void IptcProfile_DeepClone_WithLatin1Data_DoesNotWriteUtf8EnvelopeRecord()
    {
        IptcProfile profile = new();
        profile.SetValue(IptcTag.City, Encoding.Latin1, "ESPAÑA");
        IptcProfile clone = profile.DeepClone();

        clone.UpdateData();

        // Only the 2:90 City dataset is present. Latin-1 stores Ñ as D1, without a UTF-8 envelope.
        byte[] expectedData = [28, 2, 90, 0, 6, 69, 83, 80, 65, 209, 65];
        Assert.Equal(expectedData, clone.Data);
    }

    /// <summary>
    /// Writes one UTF-8 declaration after repeated JPEG saves and image clones.
    /// </summary>
    [Fact]
    public void WritingClonedImage_WithUtf8Data_WritesEnvelopeRecordOnce()
    {
        // arrange
        using Image<Rgba32> image = Image.Load<Rgba32>(TestFile.Create(TestImages.Png.TestPattern31x31).Bytes);
        image.Metadata.IptcProfile = new IptcProfile();
        image.Metadata.IptcProfile.SetValue(IptcTag.Caption, "ESPAÑA");

        // 1:90 declares UTF-8. The 2:120 Caption dataset contains the seven UTF-8 bytes for ESPAÑA.
        byte[] expectedData = [28, 1, 90, 0, 3, 27, 37, 71, 28, 2, 120, 0, 7, 69, 83, 80, 65, 195, 145, 65];

        // act
        using Image<Rgba32> firstClone = image.Clone();
        using Image<Rgba32> once = WriteAndReadJpeg(firstClone);

        using Image<Rgba32> secondClone = once.Clone();
        using Image<Rgba32> twice = WriteAndReadJpeg(secondClone);

        // assert
        IptcProfile actual = twice.Metadata.IptcProfile;
        Assert.Equal(expectedData, actual.Data);
        IptcValue value = Assert.Single(actual.Values);
        Assert.Equal("ESPAÑA", value.Value);
    }

    [Theory]
    [MemberData(nameof(AllIptcTags))]
    public void IptcProfile_SetValue_WithStrictEnabled_Works(IptcTag tag)
    {
        // arrange
        IptcProfile profile = new();
        string value = new('s', tag.MaxLength() + 1);
        int expectedLength = tag.MaxLength();

        // act
        profile.SetValue(tag, value);

        // assert
        IptcValue actual = profile.GetValues(tag).First();
        Assert.Equal(expectedLength, actual.Value.Length);
    }

    [Theory]
    [MemberData(nameof(AllIptcTags))]
    public void IptcProfile_SetValue_WithStrictDisabled_Works(IptcTag tag)
    {
        // arrange
        IptcProfile profile = new();
        string value = new('s', tag.MaxLength() + 1);
        int expectedLength = value.Length;

        // act
        profile.SetValue(tag, value, false);

        // assert
        IptcValue actual = profile.GetValues(tag).First();
        Assert.Equal(expectedLength, actual.Value.Length);
    }

    [Theory]
    [InlineData(IptcTag.DigitalCreationDate)]
    [InlineData(IptcTag.ExpirationDate)]
    [InlineData(IptcTag.CreatedDate)]
    [InlineData(IptcTag.ReferenceDate)]
    [InlineData(IptcTag.ReleaseDate)]
    public void IptcProfile_SetDateValue_Works(IptcTag tag)
    {
        // arrange
        IptcProfile profile = new();
        DateTimeOffset datetime = new(new DateTime(1994, 3, 17));

        // act
        profile.SetDateTimeValue(tag, datetime);

        // assert
        IptcValue actual = profile.GetValues(tag).First();
        Assert.Equal("19940317", actual.Value);
    }

    [Theory]
    [InlineData(IptcTag.CreatedTime)]
    [InlineData(IptcTag.DigitalCreationTime)]
    [InlineData(IptcTag.ExpirationTime)]
    [InlineData(IptcTag.ReleaseTime)]
    public void IptcProfile_SetTimeValue_Works(IptcTag tag)
    {
        // arrange
        IptcProfile profile = new();
        DateTime dateTimeUtc = new(1994, 3, 17, 14, 15, 16, DateTimeKind.Utc);
        DateTimeOffset dateTimeOffset = new DateTimeOffset(dateTimeUtc).ToOffset(TimeSpan.FromHours(2));

        // act
        profile.SetDateTimeValue(tag, dateTimeOffset);

        // assert
        IptcValue actual = profile.GetValues(tag).First();
        Assert.Equal("161516+0200", actual.Value);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Iptc, PixelTypes.Rgba32)]
    public void ReadIptcMetadata_FromJpg_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(JpegDecoder.Instance))
        {
            Assert.NotNull(image.Metadata.IptcProfile);
            List<IptcValue> iptcValues = [.. image.Metadata.IptcProfile.Values];
            IptcProfileContainsExpectedValues(iptcValues);
        }
    }

    [Theory]
    [WithFile(TestImages.Tiff.IptcData, PixelTypes.Rgba32)]
    public void ReadIptcMetadata_FromTiff_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage(TiffDecoder.Instance))
        {
            IptcProfile iptc = image.Frames.RootFrame.Metadata.IptcProfile;
            Assert.NotNull(iptc);
            List<IptcValue> iptcValues = [.. iptc.Values];
            IptcProfileContainsExpectedValues(iptcValues);
        }
    }

    private static void IptcProfileContainsExpectedValues(List<IptcValue> iptcValues)
    {
        ContainsIptcValue(iptcValues, IptcTag.Caption, "description");
        ContainsIptcValue(iptcValues, IptcTag.CaptionWriter, "description writer");
        ContainsIptcValue(iptcValues, IptcTag.Headline, "headline");
        ContainsIptcValue(iptcValues, IptcTag.SpecialInstructions, "special instructions");
        ContainsIptcValue(iptcValues, IptcTag.Byline, "author");
        ContainsIptcValue(iptcValues, IptcTag.BylineTitle, "author title");
        ContainsIptcValue(iptcValues, IptcTag.Credit, "credits");
        ContainsIptcValue(iptcValues, IptcTag.Source, "source");
        ContainsIptcValue(iptcValues, IptcTag.Name, "title");
        ContainsIptcValue(iptcValues, IptcTag.CreatedDate, "20200414");
        ContainsIptcValue(iptcValues, IptcTag.City, "city");
        ContainsIptcValue(iptcValues, IptcTag.SubLocation, "sublocation");
        ContainsIptcValue(iptcValues, IptcTag.ProvinceState, "province-state");
        ContainsIptcValue(iptcValues, IptcTag.Country, "country");
        ContainsIptcValue(iptcValues, IptcTag.Category, "category");
        ContainsIptcValue(iptcValues, IptcTag.Urgency, "1");
        ContainsIptcValue(iptcValues, IptcTag.Keywords, "keywords");
        ContainsIptcValue(iptcValues, IptcTag.CopyrightNotice, "copyright");
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.App13WithEmptyIptc, PixelTypes.Rgba32)]
    public void ReadApp13_WithEmptyIptc_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(JpegDecoder.Instance);
        Assert.Null(image.Metadata.IptcProfile);
    }

    [Fact]
    public void IptcProfile_ToAndFromByteArray_Works()
    {
        // arrange
        IptcProfile profile = new();
        const string expectedCaptionWriter = "unittest";
        const string expectedCaption = "test";
        profile.SetValue(IptcTag.CaptionWriter, expectedCaptionWriter);
        profile.SetValue(IptcTag.Caption, expectedCaption);

        // act
        profile.UpdateData();
        byte[] profileBytes = profile.Data;
        IptcProfile profileFromBytes = new(profileBytes);

        // assert
        List<IptcValue> iptcValues = [.. profileFromBytes.Values];
        ContainsIptcValue(iptcValues, IptcTag.CaptionWriter, expectedCaptionWriter);
        ContainsIptcValue(iptcValues, IptcTag.Caption, expectedCaption);
    }

    [Fact]
    public void IptcProfile_CloneIsDeep()
    {
        // arrange
        IptcProfile profile = new();
        const string captionWriter = "unittest";
        const string caption = "test";
        profile.SetValue(IptcTag.CaptionWriter, captionWriter);
        profile.SetValue(IptcTag.Caption, caption);

        // act
        IptcProfile clone = profile.DeepClone();
        clone.SetValue(IptcTag.Caption, "changed");

        // assert
        Assert.Equal(2, clone.Values.Count());
        List<IptcValue> cloneValues = [.. clone.Values];
        ContainsIptcValue(cloneValues, IptcTag.CaptionWriter, captionWriter);
        ContainsIptcValue(cloneValues, IptcTag.Caption, "changed");
        ContainsIptcValue([.. profile.Values], IptcTag.Caption, caption);
    }

    [Fact]
    public void IptcValue_CloneIsDeep()
    {
        // arrange
        IptcValue iptcValue = new(IptcTag.Caption, System.Text.Encoding.UTF8, "test", true);

        // act
        IptcValue clone = iptcValue.DeepClone();
        clone.Value = "changed";

        // assert
        Assert.NotEqual(iptcValue.Value, clone.Value);
    }

    [Fact]
    public void WritingImage_PreservesIptcProfile()
    {
        // arrange
        using Image<Rgba32> image = Image.Load<Rgba32>(TestFile.Create(TestImages.Png.TestPattern31x31).Bytes);
        image.Metadata.IptcProfile = new IptcProfile();
        const string expectedCaptionWriter = "unittest";
        const string expectedCaption = "test";
        image.Metadata.IptcProfile.SetValue(IptcTag.CaptionWriter, expectedCaptionWriter);
        image.Metadata.IptcProfile.SetValue(IptcTag.Caption, expectedCaption);

        // act
        using Image<Rgba32> reloadedImage = WriteAndReadJpeg(image);

        // assert
        IptcProfile actual = reloadedImage.Metadata.IptcProfile;
        Assert.NotNull(actual);
        List<IptcValue> iptcValues = [.. actual.Values];
        ContainsIptcValue(iptcValues, IptcTag.CaptionWriter, expectedCaptionWriter);
        ContainsIptcValue(iptcValues, IptcTag.Caption, expectedCaption);
    }

    [Theory]
    [InlineData(IptcTag.ObjectAttribute)]
    [InlineData(IptcTag.SubjectReference)]
    [InlineData(IptcTag.SupplementalCategories)]
    [InlineData(IptcTag.Keywords)]
    [InlineData(IptcTag.LocationCode)]
    [InlineData(IptcTag.LocationName)]
    [InlineData(IptcTag.ReferenceService)]
    [InlineData(IptcTag.ReferenceDate)]
    [InlineData(IptcTag.ReferenceNumber)]
    [InlineData(IptcTag.Byline)]
    [InlineData(IptcTag.BylineTitle)]
    [InlineData(IptcTag.Contact)]
    [InlineData(IptcTag.LocalCaption)]
    [InlineData(IptcTag.CaptionWriter)]
    public void IptcProfile_AddRepeatable_Works(IptcTag tag)
    {
        // arrange
        IptcProfile profile = new();
        const string expectedValue1 = "test";
        const string expectedValue2 = "another one";
        profile.SetValue(tag, expectedValue1, false);

        // act
        profile.SetValue(tag, expectedValue2, false);

        // assert
        List<IptcValue> values = [.. profile.Values];
        Assert.Equal(2, values.Count);
        ContainsIptcValue(values, tag, expectedValue1);
        ContainsIptcValue(values, tag, expectedValue2);
    }

    [Theory]
    [InlineData(IptcTag.RecordVersion)]
    [InlineData(IptcTag.ObjectType)]
    [InlineData(IptcTag.Name)]
    [InlineData(IptcTag.EditStatus)]
    [InlineData(IptcTag.EditorialUpdate)]
    [InlineData(IptcTag.Urgency)]
    [InlineData(IptcTag.Category)]
    [InlineData(IptcTag.FixtureIdentifier)]
    [InlineData(IptcTag.ReleaseDate)]
    [InlineData(IptcTag.ReleaseTime)]
    [InlineData(IptcTag.ExpirationDate)]
    [InlineData(IptcTag.ExpirationTime)]
    [InlineData(IptcTag.SpecialInstructions)]
    [InlineData(IptcTag.ActionAdvised)]
    [InlineData(IptcTag.CreatedDate)]
    [InlineData(IptcTag.CreatedTime)]
    [InlineData(IptcTag.DigitalCreationDate)]
    [InlineData(IptcTag.DigitalCreationTime)]
    [InlineData(IptcTag.OriginatingProgram)]
    [InlineData(IptcTag.ProgramVersion)]
    [InlineData(IptcTag.ObjectCycle)]
    [InlineData(IptcTag.City)]
    [InlineData(IptcTag.SubLocation)]
    [InlineData(IptcTag.ProvinceState)]
    [InlineData(IptcTag.CountryCode)]
    [InlineData(IptcTag.Country)]
    [InlineData(IptcTag.OriginalTransmissionReference)]
    [InlineData(IptcTag.Headline)]
    [InlineData(IptcTag.Credit)]
    [InlineData(IptcTag.CopyrightNotice)]
    [InlineData(IptcTag.Caption)]
    [InlineData(IptcTag.ImageType)]
    [InlineData(IptcTag.ImageOrientation)]
    public void IptcProfile_AddNoneRepeatable_DoesOverrideOldValue(IptcTag tag)
    {
        // arrange
        IptcProfile profile = new();
        const string expectedValue = "another one";
        profile.SetValue(tag, "test", false);

        // act
        profile.SetValue(tag, expectedValue, false);

        // assert
        List<IptcValue> values = [.. profile.Values];
        Assert.Equal(1, values.Count);
        ContainsIptcValue(values, tag, expectedValue);
    }

    [Fact]
    public void IptcProfile_RemoveByTag_RemovesAllEntrys()
    {
        // arrange
        IptcProfile profile = new();
        profile.SetValue(IptcTag.Byline, "test");
        profile.SetValue(IptcTag.Byline, "test2");

        // act
        bool result = profile.RemoveValue(IptcTag.Byline);

        // assert
        Assert.True(result, "removed result should be true");
        Assert.Empty(profile.Values);
    }

    [Fact]
    public void IptcProfile_RemoveByTagAndValue_Works()
    {
        // arrange
        IptcProfile profile = new();
        profile.SetValue(IptcTag.Byline, "test");
        profile.SetValue(IptcTag.Byline, "test2");

        // act
        bool result = profile.RemoveValue(IptcTag.Byline, "test2");

        // assert
        Assert.True(result, "removed result should be true");
        ContainsIptcValue([.. profile.Values], IptcTag.Byline, "test");
    }

    [Fact]
    public void IptcProfile_GetValue_RetrievesAllEntries()
    {
        // arrange
        IptcProfile profile = new();
        profile.SetValue(IptcTag.Byline, "test");
        profile.SetValue(IptcTag.Byline, "test2");
        profile.SetValue(IptcTag.Caption, "test");

        // act
        List<IptcValue> result = profile.GetValues(IptcTag.Byline);

        // assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    private static void ContainsIptcValue(List<IptcValue> values, IptcTag tag, string value)
    {
        Assert.True(values.Any(val => val.Tag == tag), $"Missing iptc tag {tag}");
        Assert.True(values.Contains(new IptcValue(tag, System.Text.Encoding.UTF8.GetBytes(value), false)), $"expected iptc value '{value}' was not found for tag '{tag}'");
    }

    /// <summary>
    /// Saves a JPEG and decodes it without disposing the caller's source image.
    /// </summary>
    /// <param name="image">The source image. The caller retains ownership.</param>
    /// <returns>The decoded image. The caller must dispose it.</returns>
    private static Image<Rgba32> WriteAndReadJpeg(Image<Rgba32> image)
    {
        using (MemoryStream memStream = new())
        {
            image.SaveAsJpeg(memStream);

            memStream.Position = 0;
            return Image.Load<Rgba32>(memStream);
        }
    }
}
