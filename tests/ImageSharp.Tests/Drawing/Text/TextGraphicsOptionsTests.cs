// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Text
{
    public class TextGraphicsOptionsTests
    {
        [Fact]
        public void ExplicitCastOfGraphicsOptions()
        {
            var opt = new GraphicsOptions(false)
            {
                AntialiasSubpixelDepth = 99
            };

            TextGraphicsOptions textOptions = opt;

            Assert.False(textOptions.Antialias);
            Assert.Equal(99, textOptions.AntialiasSubpixelDepth);
        }

        [Fact]
        public void ImplicitCastToGraphicsOptions()
        {
            var textOptions = new TextGraphicsOptions(false)
            {
                AntialiasSubpixelDepth = 99
            };

            var opt = (GraphicsOptions)textOptions;

            Assert.False(opt.Antialias);
            Assert.Equal(99, opt.AntialiasSubpixelDepth);
        }
    }
}