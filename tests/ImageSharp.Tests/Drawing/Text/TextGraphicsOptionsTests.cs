// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Text
{
    public class TextGraphicsOptionsTests
    {
        private readonly TextGraphicsOptions newTextGraphicsOptions = new TextGraphicsOptions();
        private readonly TextGraphicsOptions cloneTextGraphicsOptions = new TextGraphicsOptions().DeepClone();

        [Fact]
        public void CloneTextGraphicsOptionsIsNotNull() => Assert.True(this.cloneTextGraphicsOptions != null);

        [Fact]
        public void DefaultTextGraphicsOptionsAntialias()
        {
            Assert.True(this.newTextGraphicsOptions.Antialias);
            Assert.True(this.cloneTextGraphicsOptions.Antialias);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsAntialiasSuppixelDepth()
        {
            const int Expected = 16;
            Assert.Equal(Expected, this.newTextGraphicsOptions.AntialiasSubpixelDepth);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.AntialiasSubpixelDepth);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsBlendPercentage()
        {
            const float Expected = 1F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.BlendPercentage);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.BlendPercentage);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsColorBlendingMode()
        {
            const PixelColorBlendingMode Expected = PixelColorBlendingMode.Normal;
            Assert.Equal(Expected, this.newTextGraphicsOptions.ColorBlendingMode);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.ColorBlendingMode);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsAlphaCompositionMode()
        {
            const PixelAlphaCompositionMode Expected = PixelAlphaCompositionMode.SrcOver;
            Assert.Equal(Expected, this.newTextGraphicsOptions.AlphaCompositionMode);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.AlphaCompositionMode);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsApplyKerning()
        {
            const bool Expected = true;
            Assert.Equal(Expected, this.newTextGraphicsOptions.ApplyKerning);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.ApplyKerning);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsHorizontalAlignment()
        {
            const HorizontalAlignment Expected = HorizontalAlignment.Left;
            Assert.Equal(Expected, this.newTextGraphicsOptions.HorizontalAlignment);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.HorizontalAlignment);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsVerticalAlignment()
        {
            const VerticalAlignment Expected = VerticalAlignment.Top;
            Assert.Equal(Expected, this.newTextGraphicsOptions.VerticalAlignment);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.VerticalAlignment);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsDpiX()
        {
            const float Expected = 72F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.DpiX);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.DpiX);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsDpiY()
        {
            const float Expected = 72F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.DpiY);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.DpiY);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsTabWidth()
        {
            const float Expected = 4F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.TabWidth);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.TabWidth);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsWrapTextWidth()
        {
            const float Expected = 0F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.WrapTextWidth);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.WrapTextWidth);
        }

        [Fact]
        public void NonDefaultClone()
        {
            var expected = new TextGraphicsOptions
            {
                AlphaCompositionMode = PixelAlphaCompositionMode.DestAtop,
                Antialias = false,
                AntialiasSubpixelDepth = 23,
                ApplyKerning = false,
                BlendPercentage = .25F,
                ColorBlendingMode = PixelColorBlendingMode.HardLight,
                DpiX = 46F,
                DpiY = 52F,
                HorizontalAlignment = HorizontalAlignment.Center,
                TabWidth = 3F,
                VerticalAlignment = VerticalAlignment.Bottom,
                WrapTextWidth = 42F
            };

            TextGraphicsOptions actual = expected.DeepClone();

            Assert.Equal(expected.AlphaCompositionMode, actual.AlphaCompositionMode);
            Assert.Equal(expected.Antialias, actual.Antialias);
            Assert.Equal(expected.AntialiasSubpixelDepth, actual.AntialiasSubpixelDepth);
            Assert.Equal(expected.ApplyKerning, actual.ApplyKerning);
            Assert.Equal(expected.BlendPercentage, actual.BlendPercentage);
            Assert.Equal(expected.ColorBlendingMode, actual.ColorBlendingMode);
            Assert.Equal(expected.DpiX, actual.DpiX);
            Assert.Equal(expected.DpiY, actual.DpiY);
            Assert.Equal(expected.HorizontalAlignment, actual.HorizontalAlignment);
            Assert.Equal(expected.TabWidth, actual.TabWidth);
            Assert.Equal(expected.VerticalAlignment, actual.VerticalAlignment);
            Assert.Equal(expected.WrapTextWidth, actual.WrapTextWidth);
        }

        [Fact]
        public void CloneIsDeep()
        {
            var expected = new TextGraphicsOptions();
            TextGraphicsOptions actual = expected.DeepClone();

            actual.AlphaCompositionMode = PixelAlphaCompositionMode.DestAtop;
            actual.Antialias = false;
            actual.AntialiasSubpixelDepth = 23;
            actual.ApplyKerning = false;
            actual.BlendPercentage = .25F;
            actual.ColorBlendingMode = PixelColorBlendingMode.HardLight;
            actual.DpiX = 46F;
            actual.DpiY = 52F;
            actual.HorizontalAlignment = HorizontalAlignment.Center;
            actual.TabWidth = 3F;
            actual.VerticalAlignment = VerticalAlignment.Bottom;
            actual.WrapTextWidth = 42F;

            Assert.NotEqual(expected.AlphaCompositionMode, actual.AlphaCompositionMode);
            Assert.NotEqual(expected.Antialias, actual.Antialias);
            Assert.NotEqual(expected.AntialiasSubpixelDepth, actual.AntialiasSubpixelDepth);
            Assert.NotEqual(expected.ApplyKerning, actual.ApplyKerning);
            Assert.NotEqual(expected.BlendPercentage, actual.BlendPercentage);
            Assert.NotEqual(expected.ColorBlendingMode, actual.ColorBlendingMode);
            Assert.NotEqual(expected.DpiX, actual.DpiX);
            Assert.NotEqual(expected.DpiY, actual.DpiY);
            Assert.NotEqual(expected.HorizontalAlignment, actual.HorizontalAlignment);
            Assert.NotEqual(expected.TabWidth, actual.TabWidth);
            Assert.NotEqual(expected.VerticalAlignment, actual.VerticalAlignment);
            Assert.NotEqual(expected.WrapTextWidth, actual.WrapTextWidth);
        }

        [Fact]
        public void ExplicitCastOfGraphicsOptions()
        {
            TextGraphicsOptions textOptions = new GraphicsOptions
            {
                Antialias = false,
                AntialiasSubpixelDepth = 99
            };

            Assert.False(textOptions.Antialias);
            Assert.Equal(99, textOptions.AntialiasSubpixelDepth);
        }

        [Fact]
        public void ImplicitCastToGraphicsOptions()
        {
            var textOptions = new TextGraphicsOptions
            {
                Antialias = false,
                AntialiasSubpixelDepth = 99
            };

            var opt = (GraphicsOptions)textOptions;

            Assert.False(opt.Antialias);
            Assert.Equal(99, opt.AntialiasSubpixelDepth);
        }
    }
}
