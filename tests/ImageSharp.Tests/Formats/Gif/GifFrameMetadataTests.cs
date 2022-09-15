// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Gif;

namespace SixLabors.ImageSharp.Tests.Formats.Gif;

[Trait("Format", "Gif")]
public class GifFrameMetadataTests
{
    [Fact]
    public void CloneIsDeep()
    {
        var meta = new GifFrameMetadata
        {
            FrameDelay = 1,
            DisposalMethod = GifDisposalMethod.RestoreToBackground,
            ColorTableLength = 2
        };

        var clone = (GifFrameMetadata)meta.DeepClone();

        clone.FrameDelay = 2;
        clone.DisposalMethod = GifDisposalMethod.RestoreToPrevious;
        clone.ColorTableLength = 1;

        Assert.False(meta.FrameDelay.Equals(clone.FrameDelay));
        Assert.False(meta.DisposalMethod.Equals(clone.DisposalMethod));
        Assert.False(meta.ColorTableLength.Equals(clone.ColorTableLength));
    }
}
