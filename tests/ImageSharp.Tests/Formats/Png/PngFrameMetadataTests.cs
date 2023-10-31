// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class PngFrameMetadataTests
{
    [Fact]
    public void CloneIsDeep()
    {
        PngFrameMetadata meta = new()
        {
            FrameDelay = new(1, 0),
            DisposalMethod = PngDisposalMethod.Background,
            BlendMethod = PngBlendMethod.Over,
        };

        PngFrameMetadata clone = (PngFrameMetadata)meta.DeepClone();

        Assert.True(meta.FrameDelay.Equals(clone.FrameDelay));
        Assert.True(meta.DisposalMethod.Equals(clone.DisposalMethod));
        Assert.True(meta.BlendMethod.Equals(clone.BlendMethod));

        clone.FrameDelay = new(2, 1);
        clone.DisposalMethod = PngDisposalMethod.Previous;
        clone.BlendMethod = PngBlendMethod.Source;

        Assert.False(meta.FrameDelay.Equals(clone.FrameDelay));
        Assert.False(meta.DisposalMethod.Equals(clone.DisposalMethod));
        Assert.False(meta.BlendMethod.Equals(clone.BlendMethod));
    }
}
