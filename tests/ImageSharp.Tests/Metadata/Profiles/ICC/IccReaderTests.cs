// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccReaderTests
{
    [Theory]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.Rgb24, 10, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 560)]
    [WithFile(TestImages.Jpeg.ICC.AppleRGB, PixelTypes.Rgb24, 10, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 552)]
    [WithFile(TestImages.Jpeg.ICC.ColorMatch, PixelTypes.Rgb24, 10, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 560)]
    [WithFile(TestImages.Jpeg.ICC.WideRGB, PixelTypes.Rgb24, 10, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 560)]
    [WithFile(TestImages.Jpeg.ICC.SRgb, PixelTypes.Rgb24, 17, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 3144)]
    [WithFile(TestImages.Jpeg.ICC.ProPhoto, PixelTypes.Rgb24, 12, IccColorSpaceType.Rgb, IccColorSpaceType.CieXyz, 940)]
    [WithFile(TestImages.Jpeg.ICC.CMYK, PixelTypes.Rgb24, 10, IccColorSpaceType.Cmyk, IccColorSpaceType.CieLab, 557168)]
    public void ReadProfile_Works<TPixel>(TestImageProvider<TPixel> provider, int expectedEntries, IccColorSpaceType expectedDataColorSpace, IccColorSpaceType expectedConnectionSpace, uint expectedDataSize)
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

    [Fact]
    public void ReadProfile_NoEntries()
    {
        IccProfile output = IccReader.Read(IccTestDataProfiles.HeaderRandomArray);

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
}
