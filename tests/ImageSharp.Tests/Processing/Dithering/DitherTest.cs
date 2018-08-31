// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization
{
    public class DitherTest : BaseImageOperationsExtensionTest
    {
        private readonly IOrderedDither orderedDither;
        private readonly IErrorDiffuser errorDiffuser;
        private readonly Rgba32[] TestPalette =
        {
            Rgba32.Red,
            Rgba32.Green,
            Rgba32.Blue
        };

        public DitherTest()
        {
            this.orderedDither = KnownDitherers.BayerDither4x4;
            this.errorDiffuser = KnownDiffusers.FloydSteinberg;
        }

        [Fact]
        public void Dither_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither);
            OrderedDitherPaletteProcessor<Rgba32> p = this.Verify<OrderedDitherPaletteProcessor<Rgba32>>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(NamedColors<Rgba32>.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_rect_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.rect);
            OrderedDitherPaletteProcessor<Rgba32> p = this.Verify<OrderedDitherPaletteProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(NamedColors<Rgba32>.WebSafePalette, p.Palette);
        }
        [Fact]
        public void Dither_index_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.TestPalette);
            OrderedDitherPaletteProcessor<Rgba32> p = this.Verify<OrderedDitherPaletteProcessor<Rgba32>>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(this.TestPalette, p.Palette);
        }

        [Fact]
        public void Dither_index_rect_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.TestPalette, this.rect);
            OrderedDitherPaletteProcessor<Rgba32> p = this.Verify<OrderedDitherPaletteProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(this.TestPalette, p.Palette);
        }


        [Fact]
        public void Dither_ErrorDiffuser_CorrectProcessor()
        {
            this.operations.Diffuse(this.errorDiffuser, .4F);
            ErrorDiffusionPaletteProcessor<Rgba32> p = this.Verify<ErrorDiffusionPaletteProcessor<Rgba32>>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.4F, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_rect_CorrectProcessor()
        {
            this.operations.Diffuse(this.errorDiffuser, .3F, this.rect);
            ErrorDiffusionPaletteProcessor<Rgba32> p = this.Verify<ErrorDiffusionPaletteProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.3F, p.Threshold);
            Assert.Equal(NamedColors<Rgba32>.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_CorrectProcessorWithColors()
        {
            this.operations.Diffuse(this.errorDiffuser, .5F, this.TestPalette);
            ErrorDiffusionPaletteProcessor<Rgba32> p = this.Verify<ErrorDiffusionPaletteProcessor<Rgba32>>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.5F, p.Threshold);
            Assert.Equal(this.TestPalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_rect_CorrectProcessorWithColors()
        {
            this.operations.Diffuse(this.errorDiffuser, .5F, this.TestPalette, this.rect);
            ErrorDiffusionPaletteProcessor<Rgba32> p = this.Verify<ErrorDiffusionPaletteProcessor<Rgba32>>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.5F, p.Threshold);
            Assert.Equal(this.TestPalette, p.Palette);
        }
    }
}