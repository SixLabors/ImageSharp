// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Drawing;
using SixLabors.Fonts;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Text
{
    public class TextGraphicsOptionsTests
    {
        [Fact]
        public void ExplicitCastOfGraphicsOptions()
        {
            GraphicsOptions opt = new GraphicsOptions(false)
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
            TextGraphicsOptions textOptions = new TextGraphicsOptions(false)
            {
                AntialiasSubpixelDepth = 99
            };

            GraphicsOptions opt = (GraphicsOptions)textOptions;

            Assert.False(opt.Antialias);
            Assert.Equal(99, opt.AntialiasSubpixelDepth);
        }
    }
}