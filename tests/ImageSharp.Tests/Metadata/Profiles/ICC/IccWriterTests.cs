// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccWriterTests
{
    [Fact]
    public void WriteProfile_NoEntries()
    {
        IccProfile profile = new()
        {
            Header = IccTestDataProfiles.HeaderRandomWrite
        };
        byte[] output = IccWriter.Write(profile);

        Assert.Equal(IccTestDataProfiles.HeaderRandomArray, output);
    }

    [Fact]
    public void WriteProfile_DuplicateEntry()
    {
        byte[] output = IccWriter.Write(IccTestDataProfiles.ProfileRandomVal);

        Assert.Equal(IccTestDataProfiles.ProfileRandomArray, output);
    }
}
