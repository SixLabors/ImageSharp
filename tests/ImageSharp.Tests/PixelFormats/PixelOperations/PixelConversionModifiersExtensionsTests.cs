// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    [Trait("Category", "PixelFormats")]
    public class PixelConversionModifiersExtensionsTests
    {
        [Theory]
        [InlineData(PixelConversionModifiers.None, PixelConversionModifiers.None, true)]
        [InlineData(PixelConversionModifiers.None, PixelConversionModifiers.Premultiply, false)]
        [InlineData(PixelConversionModifiers.SRgbCompand, PixelConversionModifiers.Premultiply, false)]
        [InlineData(
            PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale,
            PixelConversionModifiers.Premultiply,
            true)]
        [InlineData(
            PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale,
            PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale,
            true)]
        [InlineData(
            PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale,
            PixelConversionModifiers.Scale,
            true)]
        internal void IsDefined(
            PixelConversionModifiers baselineModifiers,
            PixelConversionModifiers checkModifiers,
            bool expected)
        {
            Assert.Equal(expected, baselineModifiers.IsDefined(checkModifiers));
        }

        [Theory]
        [InlineData(PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale | PixelConversionModifiers.SRgbCompand, PixelConversionModifiers.Scale, PixelConversionModifiers.Premultiply | PixelConversionModifiers.SRgbCompand)]
        [InlineData(PixelConversionModifiers.None, PixelConversionModifiers.Premultiply, PixelConversionModifiers.None)]
        internal void Remove(
            PixelConversionModifiers baselineModifiers,
            PixelConversionModifiers toRemove,
            PixelConversionModifiers expected)
        {
            PixelConversionModifiers result = baselineModifiers.Remove(toRemove);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(PixelConversionModifiers.Premultiply, false, PixelConversionModifiers.Premultiply)]
        [InlineData(PixelConversionModifiers.Premultiply, true, PixelConversionModifiers.Premultiply | PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Scale)]
        internal void ApplyCompanding(
            PixelConversionModifiers baselineModifiers,
            bool compand,
            PixelConversionModifiers expected)
        {
            PixelConversionModifiers result = baselineModifiers.ApplyCompanding(compand);
            Assert.Equal(expected, result);
        }
    }
}
