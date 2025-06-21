// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Exif;

[Trait("Profile", "Exif")]
public class ExifReaderTests
{
    [Fact]
    public void Read_DataIsEmpty_ReturnsEmptyCollection()
    {
        ExifReader reader = new ExifReader(Array.Empty<byte>());

        IList<IExifValue> result = reader.ReadValues();

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Read_DataIsMinimal_ReturnsEmptyCollection()
    {
        ExifReader reader = new ExifReader(new byte[] { 69, 120, 105, 102, 0, 0 });

        IList<IExifValue> result = reader.ReadValues();

        Assert.Equal(0, result.Count);
    }
}
