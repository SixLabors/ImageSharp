// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using ExifProfile = SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile;
using ExifTag = SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag;

namespace SixLabors.ImageSharp.Tests.Metadata;

/// <summary>
/// Tests the <see cref="ImageFrameMetadataTests"/> class.
/// </summary>
public class ImageFrameMetadataTests
{
    [Fact]
    public void ConstructorImageFrameMetadata()
    {
        const int frameDelay = 42;
        const int colorTableLength = 128;
        const GifDisposalMethod disposalMethod = GifDisposalMethod.RestoreToBackground;

        var metaData = new ImageFrameMetadata();
        GifFrameMetadata gifFrameMetadata = metaData.GetGifMetadata();
        gifFrameMetadata.FrameDelay = frameDelay;
        gifFrameMetadata.ColorTableLength = colorTableLength;
        gifFrameMetadata.DisposalMethod = disposalMethod;

        var clone = new ImageFrameMetadata(metaData);
        GifFrameMetadata cloneGifFrameMetadata = clone.GetGifMetadata();

        Assert.Equal(frameDelay, cloneGifFrameMetadata.FrameDelay);
        Assert.Equal(colorTableLength, cloneGifFrameMetadata.ColorTableLength);
        Assert.Equal(disposalMethod, cloneGifFrameMetadata.DisposalMethod);
    }

    [Fact]
    public void CloneIsDeep()
    {
        // arrange
        var exifProfile = new ExifProfile();
        exifProfile.SetValue(ExifTag.Software, "UnitTest");
        exifProfile.SetValue(ExifTag.Artist, "UnitTest");
        var xmpProfile = new XmpProfile(new byte[0]);
        var iccProfile = new IccProfile()
        {
            Header = new IccProfileHeader()
            {
                CmmType = "Unittest"
            }
        };
        var iptcProfile = new ImageSharp.Metadata.Profiles.Iptc.IptcProfile();
        var metaData = new ImageFrameMetadata()
        {
            XmpProfile = xmpProfile,
            ExifProfile = exifProfile,
            IccProfile = iccProfile,
            IptcProfile = iptcProfile
        };

        // act
        ImageFrameMetadata clone = metaData.DeepClone();

        // assert
        Assert.NotNull(clone);
        Assert.NotNull(clone.ExifProfile);
        Assert.NotNull(clone.XmpProfile);
        Assert.NotNull(clone.IccProfile);
        Assert.NotNull(clone.IptcProfile);
        Assert.False(metaData.ExifProfile.Equals(clone.ExifProfile));
        Assert.True(metaData.ExifProfile.Values.Count == clone.ExifProfile.Values.Count);
        Assert.False(ReferenceEquals(metaData.XmpProfile, clone.XmpProfile));
        Assert.True(metaData.XmpProfile.Data.Equals(clone.XmpProfile.Data));
        Assert.False(metaData.GetGifMetadata().Equals(clone.GetGifMetadata()));
        Assert.False(metaData.IccProfile.Equals(clone.IccProfile));
        Assert.False(metaData.IptcProfile.Equals(clone.IptcProfile));
    }
}
