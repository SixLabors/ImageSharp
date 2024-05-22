// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;

namespace SixLabors.ImageSharp.Tests.Formats.Gif.Sections;

public class GifGraphicControlExtensionTests
{
    [Fact]
    public void TestPackedValue()
    {
        Assert.Equal(0, GifGraphicControlExtension.GetPackedValue(FrameDisposalMode.Unspecified, false, false));
        Assert.Equal(11, GifGraphicControlExtension.GetPackedValue(FrameDisposalMode.RestoreToBackground, true, true));
        Assert.Equal(4, GifGraphicControlExtension.GetPackedValue(FrameDisposalMode.DoNotDispose, false, false));
        Assert.Equal(14, GifGraphicControlExtension.GetPackedValue(FrameDisposalMode.RestoreToPrevious, true, false));
    }
}
