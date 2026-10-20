// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccProfileTests
{
    /// <summary>
    /// Checks that output selection retains only compatible profiles and configures the matching return transform.
    /// </summary>
    /// <param name="profileFile">The real ICC fixture, or null for unprofiled output.</param>
    /// <param name="colorType">The selected output sample type.</param>
    /// <param name="retainProfile">Whether the original profile can be embedded.</param>
    /// <param name="requiresConversion">Whether samples require conversion from sRGB.</param>
    [Theory]
    [InlineData(null, PixelColorType.RGB, false, false)]
    [InlineData(TestIccProfiles.StandardRgbV2, PixelColorType.RGB, true, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.RGB, true, true)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.BGR | PixelColorType.Alpha, true, true)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.Indexed | PixelColorType.Alpha, true, true)]
    [InlineData(TestIccProfiles.Fogra39, PixelColorType.CMYK, true, true)]
    [InlineData(TestIccProfiles.Fogra39, PixelColorType.YCCK, true, true)]
    [InlineData(TestIccProfiles.Fogra39, PixelColorType.RGB, false, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.CMYK, false, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.Luminance, false, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.YCbCr, false, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.Other, false, false)]
    public void GetIccProfileForEncoding_SelectsProfileAndConversion(string profileFile, PixelColorType colorType, bool retainProfile, bool requiresConversion)
    {
        IccProfile profile = profileFile is null ? null : TestIccProfiles.GetProfile(profileFile);
        IccProfile outputProfile = IccProfile.GetIccProfileForEncoding(profile, colorType, MemoryAllocator.Default, out ColorProfileConverter converter);

        // Return the original object, so selection cannot replace metadata with a copy of sRGB or another profile.
        Assert.Same(retainProfile ? profile : null, outputProfile);

        if (requiresConversion)
        {
            Assert.NotNull(converter);
            Assert.Same(CompactSrgbV4Profile.Profile, converter.Options.SourceIccProfile);
            Assert.Same(outputProfile, converter.Options.TargetIccProfile);
            Assert.Same(MemoryAllocator.Default, converter.Options.MemoryAllocator);
        }
        else
        {
            Assert.Null(converter);
        }
    }

    [Theory]
    [MemberData(nameof(IccTestDataProfiles.ProfileIdTestData), MemberType = typeof(IccTestDataProfiles))]
    public void CalculateHash_WithByteArray_CalculatesProfileHash(byte[] data, IccProfileId expected)
    {
        IccProfileId result = IccProfile.CalculateHash(data);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateHash_WithByteArray_DoesNotModifyData()
    {
        byte[] data = IccTestDataProfiles.ProfileRandomArray;
        byte[] copy = new byte[data.Length];
        Buffer.BlockCopy(data, 0, copy, 0, data.Length);

        IccProfile.CalculateHash(data);

        Assert.Equal(data, copy);
    }

    [Theory]
    [MemberData(nameof(IccTestDataProfiles.ProfileValidityTestData), MemberType = typeof(IccTestDataProfiles))]
    public void CheckIsValid_WithProfiles_ReturnsValidity(byte[] data, bool expected)
    {
        IccProfile profile = new(data);

        bool result = profile.CheckIsValid();

        Assert.Equal(expected, result);
    }
}
