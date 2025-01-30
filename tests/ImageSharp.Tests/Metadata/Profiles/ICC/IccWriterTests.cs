// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccWriterTests
{
    [Fact]
    public void WriteProfile_NoEntries()
    {
        IccWriter writer = this.CreateWriter();

        IccProfile profile = new()
        {
            Header = IccTestDataProfiles.Header_Random_Write
        };
        byte[] output = IccWriter.Write(profile);

        Assert.Equal(IccTestDataProfiles.Header_Random_Array, output);
    }

    [Fact]
    public void WriteProfile_DuplicateEntry()
    {
        IccWriter writer = this.CreateWriter();

        byte[] output = IccWriter.Write(IccTestDataProfiles.Profile_Random_Val);

        Assert.Equal(IccTestDataProfiles.Profile_Random_Array, output);
    }

    private IccWriter CreateWriter()
    {
        return new();
    }
}
