// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Processing;
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

        private readonly IDither orderedDither;
        private readonly IDither errorDiffuser;
        private readonly Color[] testPalette =
        {
            Color.Red,
            Color.Green,
            Color.Blue
        };

        public DitherTest()
        {
            this.orderedDither = KnownDitherings.Bayer4x4;
            this.errorDiffuser = KnownDitherings.FloydSteinberg;
        }

        [Fact]
        public void Dither_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither);
            PaletteDitherProcessor p = this.Verify<PaletteDitherProcessor>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(Color.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_rect_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.rect);
            PaletteDitherProcessor p = this.Verify<PaletteDitherProcessor>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(Color.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_index_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.testPalette);
            PaletteDitherProcessor p = this.Verify<PaletteDitherProcessor>();
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(this.testPalette, p.Palette);
        }

        [Fact]
        public void Dither_index_rect_CorrectProcessor()
        {
            this.operations.Dither(this.orderedDither, this.testPalette, this.rect);
            PaletteDitherProcessor p = this.Verify<PaletteDitherProcessor>(this.rect);
            Assert.Equal(this.orderedDither, p.Dither);
            Assert.Equal(this.testPalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_CorrectProcessor()
        {
            this.operations.Dither(this.errorDiffuser);
            PaletteDitherProcessor p = this.Verify<PaletteDitherProcessor>();
            Assert.Equal(this.errorDiffuser, p.Dither);
            Assert.Equal(Color.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_rect_CorrectProcessor()
        {
            this.operations.Dither(this.errorDiffuser, this.rect);
            PaletteDitherProcessor p = this.Verify<PaletteDitherProcessor>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Dither);
            Assert.Equal(Color.WebSafePalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_CorrectProcessorWithColors()
        {
            this.operations.Dither(this.errorDiffuser, this.testPalette);
            PaletteDitherProcessor p = this.Verify<PaletteDitherProcessor>();
            Assert.Equal(this.errorDiffuser, p.Dither);
            Assert.Equal(this.testPalette, p.Palette);
        }

        [Fact]
        public void Dither_ErrorDiffuser_rect_CorrectProcessorWithColors()
        {
            this.operations.Dither(this.errorDiffuser, this.testPalette, this.rect);
            PaletteDitherProcessor p = this.Verify<PaletteDitherProcessor>(this.rect);
            Assert.Equal(this.errorDiffuser, p.Dither);
            Assert.Equal(this.testPalette, p.Palette);
        }
    }
}
