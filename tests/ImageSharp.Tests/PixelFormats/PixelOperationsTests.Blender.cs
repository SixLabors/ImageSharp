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
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_SrcOver), PixelBlenderMode.Normal },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Screen_SrcOver), PixelBlenderMode.Screen },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.HardLight_SrcOver), PixelBlenderMode.HardLight },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Overlay_SrcOver), PixelBlenderMode.Overlay },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Darken_SrcOver), PixelBlenderMode.Darken },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Lighten_SrcOver), PixelBlenderMode.Lighten },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Add_SrcOver), PixelBlenderMode.Add },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Subtract_SrcOver), PixelBlenderMode.Subtract },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Multiply_SrcOver), PixelBlenderMode.Multiply },

            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_Src), PixelBlenderMode.Src },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_SrcAtop), PixelBlenderMode.Atop },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_SrcOver), PixelBlenderMode.Over },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_SrcIn), PixelBlenderMode.In },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_SrcOut), PixelBlenderMode.Out },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_Dest), PixelBlenderMode.Dest },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_DestAtop), PixelBlenderMode.DestAtop },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_DestOver), PixelBlenderMode.DestOver },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_DestIn), PixelBlenderMode.DestIn },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_DestOut), PixelBlenderMode.DestOut },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_Clear), PixelBlenderMode.Clear },
            { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal_Xor), PixelBlenderMode.Xor },

            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_SrcOver), PixelBlenderMode.Normal },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Screen_SrcOver), PixelBlenderMode.Screen },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.HardLight_SrcOver), PixelBlenderMode.HardLight },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Overlay_SrcOver), PixelBlenderMode.Overlay },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Darken_SrcOver), PixelBlenderMode.Darken },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Lighten_SrcOver), PixelBlenderMode.Lighten },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Add_SrcOver), PixelBlenderMode.Add },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Subtract_SrcOver), PixelBlenderMode.Subtract },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Multiply_SrcOver), PixelBlenderMode.Multiply },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_Src), PixelBlenderMode.Src },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_SrcAtop), PixelBlenderMode.Atop },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_SrcOver), PixelBlenderMode.Over },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_SrcIn), PixelBlenderMode.In },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_SrcOut), PixelBlenderMode.Out },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_Dest), PixelBlenderMode.Dest },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_DestAtop), PixelBlenderMode.DestAtop },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_DestOver), PixelBlenderMode.DestOver },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_DestIn), PixelBlenderMode.DestIn },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_DestOut), PixelBlenderMode.DestOut },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_Clear), PixelBlenderMode.Clear },
            { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal_Xor), PixelBlenderMode.Xor },

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
