// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Gif;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    public class GifGraphicControlExtensionTests
    {
        [Fact]
        public void TestPackedValue()
        {
            Assert.Equal(0, GifGraphicControlExtension.GetPackedValue(DisposalMethod.Unspecified, false, false));
            Assert.Equal(11, GifGraphicControlExtension.GetPackedValue(DisposalMethod.RestoreToBackground, true, true));
            Assert.Equal(4, GifGraphicControlExtension.GetPackedValue(DisposalMethod.NotDispose, false, false));
            Assert.Equal(14, GifGraphicControlExtension.GetPackedValue(DisposalMethod.RestoreToPrevious, true, false));
        }
    }
}