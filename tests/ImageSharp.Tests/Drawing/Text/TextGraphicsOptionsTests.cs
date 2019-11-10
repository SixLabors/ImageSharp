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
        private readonly TextGraphicsOptions defaultTextGraphicsOptions = TextGraphicsOptions.Default;
        private readonly TextGraphicsOptions cloneTextGraphicsOptions = TextGraphicsOptions.Default.Clone();

        [Fact]
        public void DefaultTextGraphicsOptionsIsNotNull() => Assert.True(this.defaultTextGraphicsOptions != null);

        [Fact]
        public void DefaultTextGraphicsOptionsAntialias()
        {
            Assert.True(this.newTextGraphicsOptions.Antialias);
            Assert.True(this.defaultTextGraphicsOptions.Antialias);
            Assert.True(this.cloneTextGraphicsOptions.Antialias);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsAntialiasSuppixelDepth()
        {
            const int Expected = 16;
            Assert.Equal(Expected, this.newTextGraphicsOptions.AntialiasSubpixelDepth);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.AntialiasSubpixelDepth);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.AntialiasSubpixelDepth);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsBlendPercentage()
        {
            const float Expected = 1F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.BlendPercentage);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.BlendPercentage);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.BlendPercentage);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsColorBlendingMode()
        {
            const PixelColorBlendingMode Expected = PixelColorBlendingMode.Normal;
            Assert.Equal(Expected, this.newTextGraphicsOptions.ColorBlendingMode);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.ColorBlendingMode);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.ColorBlendingMode);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsAlphaCompositionMode()
        {
            const PixelAlphaCompositionMode Expected = PixelAlphaCompositionMode.SrcOver;
            Assert.Equal(Expected, this.newTextGraphicsOptions.AlphaCompositionMode);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.AlphaCompositionMode);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.AlphaCompositionMode);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsApplyKerning()
        {
            const bool Expected = true;
            Assert.Equal(Expected, this.newTextGraphicsOptions.ApplyKerning);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.ApplyKerning);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.ApplyKerning);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsHorizontalAlignment()
        {
            const HorizontalAlignment Expected = HorizontalAlignment.Left;
            Assert.Equal(Expected, this.newTextGraphicsOptions.HorizontalAlignment);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.HorizontalAlignment);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.HorizontalAlignment);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsVerticalAlignment()
        {
            const VerticalAlignment Expected = VerticalAlignment.Top;
            Assert.Equal(Expected, this.newTextGraphicsOptions.VerticalAlignment);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.VerticalAlignment);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.VerticalAlignment);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsDpiX()
        {
            const float Expected = 72F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.DpiX);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.DpiX);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.DpiX);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsDpiY()
        {
            const float Expected = 72F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.DpiY);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.DpiY);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.DpiY);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsTabWidth()
        {
            const float Expected = 4F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.TabWidth);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.TabWidth);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.TabWidth);
        }

        [Fact]
        public void DefaultTextGraphicsOptionsWrapTextWidth()
        {
            const float Expected = 0F;
            Assert.Equal(Expected, this.newTextGraphicsOptions.WrapTextWidth);
            Assert.Equal(Expected, this.defaultTextGraphicsOptions.WrapTextWidth);
            Assert.Equal(Expected, this.cloneTextGraphicsOptions.WrapTextWidth);
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
