// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccProfileTests
{
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
        var copy = new byte[data.Length];
        Buffer.BlockCopy(data, 0, copy, 0, data.Length);

        IccProfile.CalculateHash(data);

        Assert.Equal(data, copy);
    }

    [Theory]
    [MemberData(nameof(IccTestDataProfiles.ProfileValidityTestData), MemberType = typeof(IccTestDataProfiles))]
    public void CheckIsValid_WithProfiles_ReturnsValidity(byte[] data, bool expected)
    {
        var profile = new IccProfile(data);

        bool result = profile.CheckIsValid();

        Assert.Equal(expected, result);
    }
}
