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


        public static TheoryData<object, Type, PixelBlenderMode> BlenderMappings = new TheoryData<object, Type, PixelBlenderMode>()
        {
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalSrcOver), PixelBlenderMode.Normal },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.ScreenSrcOver), PixelBlenderMode.Screen },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.HardLightSrcOver), PixelBlenderMode.HardLight },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.OverlaySrcOver), PixelBlenderMode.Overlay },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.DarkenSrcOver), PixelBlenderMode.Darken },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.LightenSrcOver), PixelBlenderMode.Lighten },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.AddSrcOver), PixelBlenderMode.Add },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.SubtractSrcOver), PixelBlenderMode.Subtract },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.MultiplySrcOver), PixelBlenderMode.Multiply },

            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalSrc), PixelBlenderMode.Src },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalSrcAtop), PixelBlenderMode.Atop },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalSrcOver), PixelBlenderMode.Over },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalSrcIn), PixelBlenderMode.In },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalSrcOut), PixelBlenderMode.Out },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalDest), PixelBlenderMode.Dest },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalDestAtop), PixelBlenderMode.DestAtop },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalDestOver), PixelBlenderMode.DestOver },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalDestIn), PixelBlenderMode.DestIn },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalDestOut), PixelBlenderMode.DestOut },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalClear), PixelBlenderMode.Clear },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.NormalXor), PixelBlenderMode.Xor },

            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalSrcOver), PixelBlenderMode.Normal },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.ScreenSrcOver), PixelBlenderMode.Screen },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.HardLightSrcOver), PixelBlenderMode.HardLight },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.OverlaySrcOver), PixelBlenderMode.Overlay },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.DarkenSrcOver), PixelBlenderMode.Darken },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.LightenSrcOver), PixelBlenderMode.Lighten },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.AddSrcOver), PixelBlenderMode.Add },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.SubtractSrcOver), PixelBlenderMode.Subtract },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.MultiplySrcOver), PixelBlenderMode.Multiply },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalSrc), PixelBlenderMode.Src },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalSrcAtop), PixelBlenderMode.Atop },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalSrcOver), PixelBlenderMode.Over },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalSrcIn), PixelBlenderMode.In },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalSrcOut), PixelBlenderMode.Out },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalDest), PixelBlenderMode.Dest },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalDestAtop), PixelBlenderMode.DestAtop },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalDestOver), PixelBlenderMode.DestOver },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalDestIn), PixelBlenderMode.DestIn },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalDestOut), PixelBlenderMode.DestOut },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalClear), PixelBlenderMode.Clear },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.NormalXor), PixelBlenderMode.Xor },

        };

        [Theory]
        [MemberData(nameof(BlenderMappings))]
        public void ReturnsCorrectBlender<TPixel>(TestPixel<TPixel> pixel, Type type, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(mode);
            Assert.IsType(type, blender);
        }
    }
}
