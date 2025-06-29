// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
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
            FrameDelay = new Rational(1, 0),
            DisposalMode = FrameDisposalMode.RestoreToBackground,
            BlendMode = FrameBlendMode.Over,
        };

        PngFrameMetadata clone = meta.DeepClone();

        Assert.True(meta.FrameDelay.Equals(clone.FrameDelay));
        Assert.True(meta.DisposalMode.Equals(clone.DisposalMode));
        Assert.True(meta.BlendMode.Equals(clone.BlendMode));

        clone.FrameDelay = new Rational(2, 1);
        clone.DisposalMode = FrameDisposalMode.RestoreToPrevious;
        clone.BlendMode = FrameBlendMode.Source;

        Assert.False(meta.FrameDelay.Equals(clone.FrameDelay));
        Assert.False(meta.DisposalMode.Equals(clone.DisposalMode));
        Assert.False(meta.BlendMode.Equals(clone.BlendMode));
    }
}
