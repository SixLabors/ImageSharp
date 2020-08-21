// Copyright (c) Six Labors.
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

        [Fact]
        public void ErrorDitherEquality()
        {
            IDither dither = KnownDitherings.FloydSteinberg;
            ErrorDither dither2 = ErrorDither.FloydSteinberg;
            ErrorDither dither3 = ErrorDither.FloydSteinberg;

            Assert.True(dither == dither2);
            Assert.True(dither2 == dither);
            Assert.False(dither != dither2);
            Assert.False(dither2 != dither);
            Assert.Equal(dither, dither2);
            Assert.Equal(dither, (object)dither2);
            Assert.Equal(dither.GetHashCode(), dither2.GetHashCode());

            dither = null;
            Assert.False(dither == dither2);
            Assert.False(dither2 == dither);
            Assert.True(dither != dither2);
            Assert.True(dither2 != dither);
            Assert.NotEqual(dither, dither2);
            Assert.NotEqual(dither, (object)dither2);
            Assert.NotEqual(dither?.GetHashCode(), dither2.GetHashCode());

            Assert.True(dither2 == dither3);
            Assert.True(dither3 == dither2);
            Assert.False(dither2 != dither3);
            Assert.False(dither3 != dither2);
            Assert.Equal(dither2, dither3);
            Assert.Equal(dither2, (object)dither3);
            Assert.Equal(dither2.GetHashCode(), dither3.GetHashCode());
        }

        [Fact]
        public void OrderedDitherEquality()
        {
            IDither dither = KnownDitherings.Bayer2x2;
            OrderedDither dither2 = OrderedDither.Bayer2x2;
            OrderedDither dither3 = OrderedDither.Bayer2x2;

            Assert.True(dither == dither2);
            Assert.True(dither2 == dither);
            Assert.False(dither != dither2);
            Assert.False(dither2 != dither);
            Assert.Equal(dither, dither2);
            Assert.Equal(dither, (object)dither2);
            Assert.Equal(dither.GetHashCode(), dither2.GetHashCode());

            dither = null;
            Assert.False(dither == dither2);
            Assert.False(dither2 == dither);
            Assert.True(dither != dither2);
            Assert.True(dither2 != dither);
            Assert.NotEqual(dither, dither2);
            Assert.NotEqual(dither, (object)dither2);
            Assert.NotEqual(dither?.GetHashCode(), dither2.GetHashCode());

            Assert.True(dither2 == dither3);
            Assert.True(dither3 == dither2);
            Assert.False(dither2 != dither3);
            Assert.False(dither3 != dither2);
            Assert.Equal(dither2, dither3);
            Assert.Equal(dither2, (object)dither3);
            Assert.Equal(dither2.GetHashCode(), dither3.GetHashCode());
        }
    }
}
