// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;

namespace SixLabors.ImageSharp.Tests.Formats.Gif;

[Trait("Format", "Gif")]
public class GifFrameMetadataTests
{
    [Fact]
    public void CloneIsDeep()
    {
        GifFrameMetadata meta = new()
        {
            FrameDelay = 1,
            DisposalMode = FrameDisposalMode.RestoreToBackground,
            LocalColorTable = new[] { Color.Black, Color.White }
        };

        GifFrameMetadata clone = (GifFrameMetadata)meta.DeepClone();

        clone.FrameDelay = 2;
        clone.DisposalMode = FrameDisposalMode.RestoreToPrevious;
        clone.LocalColorTable = new[] { Color.Black };

        Assert.False(meta.FrameDelay.Equals(clone.FrameDelay));
        Assert.False(meta.DisposalMode.Equals(clone.DisposalMode));
        Assert.False(meta.LocalColorTable.Value.Length == clone.LocalColorTable.Value.Length);
        Assert.Equal(1, clone.LocalColorTable.Value.Length);
    }
}
