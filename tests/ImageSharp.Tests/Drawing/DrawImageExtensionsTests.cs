// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.ImageSharp.Tests.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class DrawImageExtensionsTests : BaseImageOperationsExtensionTest
    {

        [Fact]
        public void DrawImage_OpacityOnly_VerifyGraphicOptionsTakenFromContext()
        {
            // non-default values as we cant easly defect usage otherwise
            this.options.AlphaCompositionMode = PixelAlphaCompositionMode.Xor;
            this.options.ColorBlendingMode = PixelColorBlendingMode.Screen;

            this.operations.DrawImage(null, 0.5f);
            var dip = this.Verify<DrawImageProcessor>();

            Assert.Equal(0.5, dip.Opacity);
            Assert.Equal(this.options.AlphaCompositionMode, dip.AlphaCompositionMode);
            Assert.Equal(this.options.ColorBlendingMode, dip.ColorBlendingMode);
        }

        [Fact]
        public void DrawImage_OpacityAndBlending_VerifyGraphicOptionsTakenFromContext()
        {
            // non-default values as we cant easly defect usage otherwise
            this.options.AlphaCompositionMode = PixelAlphaCompositionMode.Xor;
            this.options.ColorBlendingMode = PixelColorBlendingMode.Screen;

            this.operations.DrawImage(null, PixelColorBlendingMode.Multiply, 0.5f);
            var dip = this.Verify<DrawImageProcessor>();

            Assert.Equal(0.5, dip.Opacity);
            Assert.Equal(this.options.AlphaCompositionMode, dip.AlphaCompositionMode);
            Assert.Equal(PixelColorBlendingMode.Multiply, dip.ColorBlendingMode);
        }

        [Fact]
        public void DrawImage_LocationAndOpacity_VerifyGraphicOptionsTakenFromContext()
        {
            // non-default values as we cant easly defect usage otherwise
            this.options.AlphaCompositionMode = PixelAlphaCompositionMode.Xor;
            this.options.ColorBlendingMode = PixelColorBlendingMode.Screen;

            this.operations.DrawImage(null, Point.Empty, 0.5f);
            var dip = this.Verify<DrawImageProcessor>();

            Assert.Equal(0.5, dip.Opacity);
            Assert.Equal(this.options.AlphaCompositionMode, dip.AlphaCompositionMode);
            Assert.Equal(this.options.ColorBlendingMode, dip.ColorBlendingMode);
        }

        [Fact]
        public void DrawImage_LocationAndOpacityAndBlending_VerifyGraphicOptionsTakenFromContext()
        {
            // non-default values as we cant easly defect usage otherwise
            this.options.AlphaCompositionMode = PixelAlphaCompositionMode.Xor;
            this.options.ColorBlendingMode = PixelColorBlendingMode.Screen;

            this.operations.DrawImage(null, Point.Empty, PixelColorBlendingMode.Multiply, 0.5f);
            var dip = this.Verify<DrawImageProcessor>();

            Assert.Equal(0.5, dip.Opacity);
            Assert.Equal(this.options.AlphaCompositionMode, dip.AlphaCompositionMode);
            Assert.Equal(PixelColorBlendingMode.Multiply, dip.ColorBlendingMode);
        }
    }
}
