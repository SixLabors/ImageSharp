// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class PixelBlenderTests
    {
        public static TheoryData<object, Type, PixelColorBlendingMode> BlenderMappings = new TheoryData<object, Type, PixelColorBlendingMode>()
        {
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalSrcOver), PixelColorBlendingMode.Normal },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.ScreenSrcOver), PixelColorBlendingMode.Screen },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.HardLightSrcOver), PixelColorBlendingMode.HardLight },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.OverlaySrcOver), PixelColorBlendingMode.Overlay },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.DarkenSrcOver), PixelColorBlendingMode.Darken },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.LightenSrcOver), PixelColorBlendingMode.Lighten },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.AddSrcOver), PixelColorBlendingMode.Add },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.SubtractSrcOver), PixelColorBlendingMode.Subtract },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.MultiplySrcOver), PixelColorBlendingMode.Multiply },            

            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalSrcOver), PixelColorBlendingMode.Normal },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.ScreenSrcOver), PixelColorBlendingMode.Screen },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.HardLightSrcOver), PixelColorBlendingMode.HardLight },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.OverlaySrcOver), PixelColorBlendingMode.Overlay },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.DarkenSrcOver), PixelColorBlendingMode.Darken },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.LightenSrcOver), PixelColorBlendingMode.Lighten },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.AddSrcOver), PixelColorBlendingMode.Add },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.SubtractSrcOver), PixelColorBlendingMode.Subtract },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.MultiplySrcOver), PixelColorBlendingMode.Multiply },
        };        

        [Theory]
        [MemberData(nameof(BlenderMappings))]
        public void ReturnsCorrectBlender<TPixel>(TestPixel<TPixel> pixel, Type type, PixelColorBlendingMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(mode, PixelAlphaCompositionMode.SrcOver);
            Assert.IsType(type, blender);
        }

        public static TheoryData<Rgba32, Rgba32, float, PixelColorBlendingMode, Rgba32> ColorBlendingExpectedResults = new TheoryData<Rgba32, Rgba32, float, PixelColorBlendingMode, Rgba32>()
        {
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.Normal, Rgba32.MidnightBlue },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.Screen, new Rgba32(0xFFEEE7FF) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.HardLight, new Rgba32(0xFFC62D32) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.Overlay, new Rgba32(0xFFDDCEFF) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.Darken, new Rgba32(0xFF701919) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.Lighten, new Rgba32(0xFFE1E4FF) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.Add, new Rgba32(0xFFFFFDFF) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.Subtract, new Rgba32(0xFF71CBE6) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelColorBlendingMode.Multiply, new Rgba32(0xFF631619) },

        };

        [Theory]
        [MemberData(nameof(ColorBlendingExpectedResults))]
        public void TestColorBlendingModes(Rgba32 backdrop, Rgba32 source, float opacity, PixelColorBlendingMode mode, Rgba32 expectedResult)
        {
            PixelBlender<Rgba32> blender = PixelOperations<Rgba32>.Instance.GetPixelBlender(mode, PixelAlphaCompositionMode.SrcOver);

            Rgba32 actualResult = blender.Blend(backdrop, source, opacity);

            // var str = actualResult.Rgba.ToString("X8"); // used to extract expectedResults

            Assert.Equal(actualResult.ToVector4(), expectedResult.ToVector4());
        }

        public static TheoryData<Rgba32, Rgba32, float, PixelAlphaCompositionMode, Rgba32> AlphaCompositionExpectedResults = new TheoryData<Rgba32, Rgba32, float, PixelAlphaCompositionMode, Rgba32>()
        {
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.Clear, new Rgba32(0) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.Xor, new Rgba32(0) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.Dest, Rgba32.MistyRose },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.DestAtop, Rgba32.MistyRose },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.DestIn, Rgba32.MistyRose },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.DestOut, new Rgba32(0) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.DestOver, Rgba32.MistyRose },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.Src, Rgba32.MidnightBlue },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.SrcAtop, Rgba32.MidnightBlue },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.SrcIn, Rgba32.MidnightBlue },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.SrcOut, new Rgba32(0) },
            { Rgba32.MistyRose, Rgba32.MidnightBlue, 1, PixelAlphaCompositionMode.SrcOver, Rgba32.MidnightBlue },
        };

        [Theory]
        [MemberData(nameof(AlphaCompositionExpectedResults))]
        public void TestAlphaCompositionModes(Rgba32 backdrop, Rgba32 source, float opacity, PixelAlphaCompositionMode mode, Rgba32 expectedResult)
        {
            PixelBlender<Rgba32> blender = PixelOperations<Rgba32>.Instance.GetPixelBlender(PixelColorBlendingMode.Normal, mode);

            Rgba32 actualResult = blender.Blend(backdrop, source, opacity);

            // var str = actualResult.Rgba.ToString("X8"); // used to extract expectedResults

            Assert.Equal(actualResult.ToVector4(), expectedResult.ToVector4());
        }
    }
}
