// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Gif;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Gif.Sections
{
    public class GifGraphicControlExtensionTests
    {
        [Fact]
        public void TestPackedValue()
        {
            Assert.Equal(0, GifGraphicControlExtension.GetPackedValue(GifDisposalMethod.Unspecified, false, false));
            Assert.Equal(11, GifGraphicControlExtension.GetPackedValue(GifDisposalMethod.RestoreToBackground, true, true));
            Assert.Equal(4, GifGraphicControlExtension.GetPackedValue(GifDisposalMethod.NotDispose, false, false));
            Assert.Equal(14, GifGraphicControlExtension.GetPackedValue(GifDisposalMethod.RestoreToPrevious, true, false));
        }
    }
}
