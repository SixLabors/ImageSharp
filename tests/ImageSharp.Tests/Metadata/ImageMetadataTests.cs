// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Metadata;

/// <summary>
/// Tests the <see cref="ImageMetadata"/> class.
/// </summary>
public class ImageMetadataTests
{
    [Fact]
    public void ConstructorImageMetadata()
    {
        ImageMetadata metaData = new();

        ExifProfile exifProfile = new();

        metaData.ExifProfile = exifProfile;
        metaData.HorizontalResolution = 4;
        metaData.VerticalResolution = 2;

        ImageMetadata clone = metaData.DeepClone();

        Assert.Equal(exifProfile.ToByteArray(), clone.ExifProfile.ToByteArray());
        Assert.Equal(4, clone.HorizontalResolution);
        Assert.Equal(2, clone.VerticalResolution);
    }

    [Fact]
    public void CloneIsDeep()
    {
        ImageMetadata metaData = new()
        {
            ExifProfile = new(),
            HorizontalResolution = 4,
            VerticalResolution = 2
        };

        ImageMetadata clone = metaData.DeepClone();
        clone.HorizontalResolution = 2;
        clone.VerticalResolution = 4;

        Assert.False(metaData.ExifProfile.Equals(clone.ExifProfile));
        Assert.False(metaData.HorizontalResolution.Equals(clone.HorizontalResolution));
        Assert.False(metaData.VerticalResolution.Equals(clone.VerticalResolution));
    }

    [Fact]
    public void HorizontalResolution()
    {
        ImageMetadata metaData = new();
        Assert.Equal(96, metaData.HorizontalResolution);

        metaData.HorizontalResolution = 0;
        Assert.Equal(96, metaData.HorizontalResolution);

        metaData.HorizontalResolution = -1;
        Assert.Equal(96, metaData.HorizontalResolution);

        metaData.HorizontalResolution = 1;
        Assert.Equal(1, metaData.HorizontalResolution);
    }

    [Fact]
    public void VerticalResolution()
    {
        ImageMetadata metaData = new();
        Assert.Equal(96, metaData.VerticalResolution);

        metaData.VerticalResolution = 0;
        Assert.Equal(96, metaData.VerticalResolution);

        metaData.VerticalResolution = -1;
        Assert.Equal(96, metaData.VerticalResolution);

        metaData.VerticalResolution = 1;
        Assert.Equal(1, metaData.VerticalResolution);
    }

    [Fact]
    public void SyncProfiles()
    {
        ExifProfile exifProfile = new();
        exifProfile.SetValue(ExifTag.XResolution, new(200));
        exifProfile.SetValue(ExifTag.YResolution, new(300));

        using Image<Rgba32> image = new(1, 1);
        image.Metadata.ExifProfile = exifProfile;
        image.Metadata.HorizontalResolution = 400;
        image.Metadata.VerticalResolution = 500;

        using MemoryStream memoryStream = new();
        image.SaveAsBmp(memoryStream);

        Assert.Equal(400, image.Metadata.ExifProfile.GetValue(ExifTag.XResolution).Value.ToDouble());
        Assert.Equal(500, image.Metadata.ExifProfile.GetValue(ExifTag.YResolution).Value.ToDouble());
    }
}
