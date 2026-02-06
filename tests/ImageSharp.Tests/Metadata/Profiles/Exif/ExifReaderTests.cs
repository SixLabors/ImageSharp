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
        ExifReader reader = new(Array.Empty<byte>());

        IList<IExifValue> result = reader.ReadValues();

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Read_DataIsMinimal_ReturnsEmptyCollection()
    {
        ExifReader reader = new("Exif\0\0"u8.ToArray());

        IList<IExifValue> result = reader.ReadValues();

        Assert.Equal(0, result.Count);
    }
}
