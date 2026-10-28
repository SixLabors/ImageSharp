// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccReaderTests
{
    /// <summary>
    /// Checks that the length limit applies to all segments and that a rejected segment consumes no input.
    /// </summary>
    [Fact]
    public void Read_LaterSegmentExceedsLimit_ThrowsBeforeReading()
    {
        byte[] data = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2).ToByteArray();
        using MemoryStream stream = new(data);
        IccReader reader = new(data.Length);
        int firstSegmentLength = data.Length / 2;
        int remainingLength = data.Length - firstSegmentLength;

        Assert.Equal(firstSegmentLength, reader.Read(stream, firstSegmentLength));

        // Each request fits the limit on its own, but this second request would exceed the combined limit by one byte.
        Assert.Throws<InvalidIccProfileException>(() => reader.Read(stream, remainingLength + 1));
        Assert.Equal((long)firstSegmentLength, stream.Position);

        // The rejected request must leave the reader usable. The remaining bytes reach the limit exactly and complete the real profile.
        Assert.Equal(remainingLength, reader.Read(stream, remainingLength));
        Assert.Equal(data, reader.ToProfile().ToByteArray());
    }

    /// <summary>
    /// Checks that a short segment returns its actual byte count and cannot produce a complete profile.
    /// </summary>
    [Fact]
    public void Read_ShortSegment_ReturnsActualByteCount()
    {
        byte[] data = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2).ToByteArray();

        // The real profile still declares its full size, but the stream ends one byte before that boundary.
        using MemoryStream stream = new(data, 0, data.Length - 1, writable: false);
        IccReader reader = new(data.Length);

        Assert.Equal(data.Length - 1, reader.Read(stream, data.Length));
        Assert.Throws<InvalidIccProfileException>(() => reader.ToProfile());
    }

    /// <summary>
    /// Checks that an incomplete header or tag count is rejected before profile parsing.
    /// </summary>
    /// <param name="length">The number of bytes available from the incomplete profile.</param>
    [Theory]
    [InlineData(127)] // The 128-byte header is missing its last byte.
    [InlineData(131)] // The four-byte tag count after the header is missing its last byte.
    public void ToProfile_WithIncompleteHeader_Throws(int length)
    {
        byte[] data = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2).ToByteArray();
        using MemoryStream stream = new(data, 0, length, writable: false);
        IccReader reader = new();

        Assert.Equal(length, reader.Read(stream, length));
        Assert.Throws<InvalidIccProfileException>(() => reader.ToProfile());
    }

    /// <summary>
    /// Checks that the complete declared tag table must fit inside the retained profile.
    /// </summary>
    [Fact]
    public void ToProfile_WithIncompleteTagTable_Throws()
    {
        byte[] data = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2).ToByteArray();

        // The tag count follows the 128-byte header. Each table entry stores three uints: signature, offset and size.
        const int headerLength = 128;
        uint tagCount = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(headerLength));
        int length = headerLength + sizeof(uint) + ((int)tagCount * 3 * sizeof(uint)) - 1;

        // Remove the final table byte and declare only the retained bytes, so rejection tests the table rather than the profile-size check.
        BinaryPrimitives.WriteUInt32BigEndian(data, (uint)length);
        using MemoryStream stream = new(data, 0, length, writable: false);
        IccReader reader = new();

        Assert.Equal(length, reader.Read(stream, length));
        Assert.Throws<InvalidIccProfileException>(() => reader.ToProfile());
    }

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.Rgb24, 10, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 560)]
    [WithFile(TestImages.Jpeg.ICC.AppleRGB, PixelTypes.Rgb24, 10, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 552)]
    [WithFile(TestImages.Jpeg.ICC.ColorMatch, PixelTypes.Rgb24, 10, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 560)]
    [WithFile(TestImages.Jpeg.ICC.WideRGB, PixelTypes.Rgb24, 10, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 560)]
    [WithFile(TestImages.Jpeg.ICC.SRgb, PixelTypes.Rgb24, 17, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 3144)]
    [WithFile(TestImages.Jpeg.ICC.ProPhoto, PixelTypes.Rgb24, 12, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 940)]
    [WithFile(TestImages.Jpeg.ICC.CMYK, PixelTypes.Rgb24, 10, IccColorSpaceType.Cmyk, IccColorSpaceType.CieLab, 557168)]
    public void ReadProfile_Works<TPixel>(
        TestImageProvider<TPixel> provider,
        int expectedEntries,
        IccColorSpaceType expectedDataColorSpace,
        IccColorSpaceType expectedConnectionSpace,
        uint expectedDataSize)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        IccProfile profile = image.Metadata.IccProfile;

        Assert.NotNull(profile);
        Assert.Equal(expectedEntries, profile.Entries.Length);
        Assert.Equal(expectedDataColorSpace, profile.Header.DataColorSpace);
        Assert.Equal(expectedConnectionSpace, profile.Header.ProfileConnectionSpace);
        Assert.Equal(expectedDataSize, profile.Header.Size);
    }

    /// <summary>
    /// Checks that bytes beyond a real profile's declared size are excluded from the retained payload.
    /// </summary>
    [Fact]
    public void ToProfile_WithTrailingData_RetainsDeclaredProfileOnly()
    {
        IccProfile source = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2);
        byte[] profileData = source.ToByteArray();

        // Append another real profile as trailing data without changing the first profile's declared size.
        byte[] data = ArrayHelper.Concat(profileData, profileData);
        using MemoryStream stream = new(data);
        IccReader reader = new();

        Assert.Equal(data.Length, reader.Read(stream, data.Length));
        IccProfile profile = reader.ToProfile();

        Assert.Equal(profileData, profile.ToByteArray());
        Assert.Equal(source.Entries.Length, profile.Entries.Length);
    }

    /// <summary>
    /// Checks that readable tag data outside the declared profile is skipped during lazy parsing.
    /// </summary>
    [Fact]
    public void ToProfile_WithTagOutsideDeclaredSize_IgnoresTag()
    {
        IccProfile source = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2);
        byte[] profileData = source.ToByteArray();
        byte[] data = ArrayHelper.Concat(profileData, profileData);

        // The first tag's offset follows the 128-byte header, four-byte tag count and four-byte tag signature.
        // Point it at the same valid tag in the appended profile: readable bytes exist, but outside the declared boundary.
        const int firstTagOffset = 128 + sizeof(uint) + sizeof(uint);
        uint originalOffset = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(firstTagOffset));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(firstTagOffset), (uint)profileData.Length + originalOffset);

        using MemoryStream stream = new(data);
        IccReader reader = new();

        Assert.Equal(data.Length, reader.Read(stream, data.Length));
        IccProfile profile = reader.ToProfile();

        // Malformed tags are skipped by the existing parser; trailing data must not make this tag acceptable.
        Assert.DoesNotContain(profile.Entries, entry => entry.TagSignature == source.Entries[0].TagSignature);
        Assert.Equal(source.Entries.Length - 1, profile.Entries.Length);
    }

    [Fact]
    public void ReadProfile_NoEntries()
    {
        byte[] data = IccTestDataProfiles.HeaderRandomArray;
        using MemoryStream stream = new(data);
        IccReader reader = new();

        reader.Read(stream, data.Length);
        IccProfile output = reader.ToProfile();

        Assert.Equal(0, output.Entries.Length);
        Assert.NotNull(output.Header);

        IccProfileHeader header = output.Header;
        IccProfileHeader expected = IccTestDataProfiles.HeaderRandomRead;
        Assert.Equal(header.Class, expected.Class);
        Assert.Equal(header.CmmType, expected.CmmType);
        Assert.Equal(header.CreationDate, expected.CreationDate);
        Assert.Equal(header.CreatorSignature, expected.CreatorSignature);
        Assert.Equal(header.DataColorSpace, expected.DataColorSpace);
        Assert.Equal(header.DeviceAttributes, expected.DeviceAttributes);
        Assert.Equal(header.DeviceManufacturer, expected.DeviceManufacturer);
        Assert.Equal(header.DeviceModel, expected.DeviceModel);
        Assert.Equal(header.FileSignature, expected.FileSignature);
        Assert.Equal(header.Flags, expected.Flags);
        Assert.Equal(header.Id, expected.Id);
        Assert.Equal(header.PcsIlluminant, expected.PcsIlluminant);
        Assert.Equal(header.PrimaryPlatformSignature, expected.PrimaryPlatformSignature);
        Assert.Equal(header.ProfileConnectionSpace, expected.ProfileConnectionSpace);
        Assert.Equal(header.RenderingIntent, expected.RenderingIntent);
        Assert.Equal(header.Size, expected.Size);
        Assert.Equal(header.Version, expected.Version);
    }

    [Fact]
    public void ReadProfile_WithUndersizedArrayTags_IgnoresTags()
    {
        const int tagCount = 100;
        const int dataOffset = 132 + (tagCount * 12);
        byte[] data = new byte[dataOffset + 16];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(128), tagCount);

        for (int i = 0; i < tagCount; i++)
        {
            Span<byte> entry = data.AsSpan(132 + (i * 12), 12);
            BinaryPrimitives.WriteUInt32BigEndian(entry, 0x73663332);
            BinaryPrimitives.WriteUInt32BigEndian(entry[4..], dataOffset);
            BinaryPrimitives.WriteUInt32BigEndian(entry[8..], 0);
        }

        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(dataOffset), 0x73663332);
        IccProfile profile = new(data);

        Assert.Empty(profile.Entries);
    }
}
