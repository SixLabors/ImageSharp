// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Formats.Ani;

[Trait("Format", "Ani")]
public class AniMetadataTests
{
    /// <summary>
    /// Verifies that resizing scales the ANI-owned encoding dimensions exactly once.
    /// </summary>
    [Fact]
    public void AfterFrameApply_ScalesEncodingDimensionsOnce()
    {
        using Image<Rgba32> image = new(32, 32);
        AniFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetAniMetadata();
        metadata.EncodingWidth = 32;
        metadata.EncodingHeight = 32;
        metadata.FrameFormat = AniFrameFormat.Cur;

        image.Mutate(context => context.Resize(64, 64));

        AniFrameMetadata resized = image.Frames.RootFrame.Metadata.GetAniMetadata();
        Assert.Equal((byte)64, resized.EncodingWidth);
        Assert.Equal((byte)64, resized.EncodingHeight);
    }
}
