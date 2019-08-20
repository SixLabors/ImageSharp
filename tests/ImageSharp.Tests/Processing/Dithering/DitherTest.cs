// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization
{
    public class DitherTest : BaseImageOperationsExtensionTest
    {
        private class Assert : Xunit.Assert
        {
            public static void Equal(ReadOnlySpan<Color> a, ReadOnlySpan<Color> b)
            {
                True(a.SequenceEqual(b));
            }
        }
        
        private readonly IOrderedDither orderedDither;
        private readonly IErrorDiffuser errorDiffuser;
        private readonly Color[] TestPalette =
        {
            Color.Red,
            Color.Green,
            Color.Blue
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
            OrderedDitherPaletteProcessor p = this.Verify<OrderedDitherPaletteProcessor>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(Color.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_rect_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.rect);
            OrderedDitherPaletteProcessor p = this.Verify<OrderedDitherPaletteProcessor>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(Color.WebSafePalette, p.Palette);
        }
        [Fact]
        public void Dither_index_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.TestPalette);
            OrderedDitherPaletteProcessor p = this.Verify<OrderedDitherPaletteProcessor>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(this.TestPalette, p.Palette);
        }

        [Fact]
        public void Dither_index_rect_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.TestPalette, this.rect);
            OrderedDitherPaletteProcessor p = this.Verify<OrderedDitherPaletteProcessor>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(this.TestPalette, p.Palette);
        }


        [Fact]
        public void Dither_ErrorDiffuser_CorrectProcessor()
        {
            this.operations.Diffuse(this.errorDiffuser, .4F);
            ErrorDiffusionPaletteProcessor p = this.Verify<ErrorDiffusionPaletteProcessor>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.4F, p.Threshold);
            Assert.Equal(Color.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_rect_CorrectProcessor()
        {
            this.operations.Diffuse(this.errorDiffuser, .3F, this.rect);
            ErrorDiffusionPaletteProcessor p = this.Verify<ErrorDiffusionPaletteProcessor>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.3F, p.Threshold);
            Assert.Equal(Color.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_CorrectProcessorWithColors()
        {
            this.operations.Diffuse(this.errorDiffuser, .5F, this.TestPalette);
            ErrorDiffusionPaletteProcessor p = this.Verify<ErrorDiffusionPaletteProcessor>();
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.5F, p.Threshold);
            Assert.Equal(this.TestPalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_rect_CorrectProcessorWithColors()
        {
            this.operations.Diffuse(this.errorDiffuser, .5F, this.TestPalette, this.rect);
            ErrorDiffusionPaletteProcessor p = this.Verify<ErrorDiffusionPaletteProcessor>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Diffuser);
            Assert.Equal(.5F, p.Threshold);
            Assert.Equal(this.TestPalette, p.Palette);
        }
    }
}
