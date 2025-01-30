// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
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
        const FrameDisposalMode disposalMethod = FrameDisposalMode.RestoreToBackground;

        ImageFrameMetadata metaData = new();
        GifFrameMetadata gifFrameMetadata = metaData.GetGifMetadata();
        gifFrameMetadata.FrameDelay = frameDelay;
        gifFrameMetadata.LocalColorTable = Enumerable.Repeat(Color.HotPink, colorTableLength).ToArray();
        gifFrameMetadata.DisposalMode = disposalMethod;

        ImageFrameMetadata clone = new(metaData);
        GifFrameMetadata cloneGifFrameMetadata = clone.GetGifMetadata();

        Assert.Equal(frameDelay, cloneGifFrameMetadata.FrameDelay);
        Assert.Equal(colorTableLength, cloneGifFrameMetadata.LocalColorTable.Value.Length);
        Assert.Equal(disposalMethod, cloneGifFrameMetadata.DisposalMode);
    }

    [Fact]
    public void CloneIsDeep()
    {
        // arrange
        ExifProfile exifProfile = new();
        exifProfile.SetValue(ExifTag.Software, "UnitTest");
        exifProfile.SetValue(ExifTag.Artist, "UnitTest");
        XmpProfile xmpProfile = new(Array.Empty<byte>());
        IccProfile iccProfile = new()
        {
            Header = new()
            {
                CmmType = "Unittest"
            }
        };
        IptcProfile iptcProfile = new();
        ImageFrameMetadata metaData = new()
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
