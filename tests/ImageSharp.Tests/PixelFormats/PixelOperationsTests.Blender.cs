// <copyright file="PixelBlenderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.PixelFormats
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using ImageSharp.PixelFormats;
    using ImageSharp.PixelFormats.PixelBlenders;
    using ImageSharp.Tests.TestUtilities;
    using Xunit;

    public partial class PixelOperationsTests
    {
        public static TheoryData<object, Type, PixelBlenderMode> BlenderMappings = new TheoryData<object, Type, PixelBlenderMode>()
                                                                                       {
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultNormalPixelBlender<Rgba32>), PixelBlenderMode.Normal },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultScreenPixelBlender<Rgba32>), PixelBlenderMode.Screen },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultHardLightPixelBlender<Rgba32>), PixelBlenderMode.HardLight },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultOverlayPixelBlender<Rgba32>), PixelBlenderMode.Overlay },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultDarkenPixelBlender<Rgba32>), PixelBlenderMode.Darken },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultLightenPixelBlender<Rgba32>), PixelBlenderMode.Lighten },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultAddPixelBlender<Rgba32>), PixelBlenderMode.Add },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultSubstractPixelBlender<Rgba32>), PixelBlenderMode.Substract },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultMultiplyPixelBlender<Rgba32>), PixelBlenderMode.Multiply },

                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultNormalPixelBlender<RgbaVector>), PixelBlenderMode.Normal },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultScreenPixelBlender<RgbaVector>), PixelBlenderMode.Screen },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultHardLightPixelBlender<RgbaVector>), PixelBlenderMode.HardLight },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultOverlayPixelBlender<RgbaVector>), PixelBlenderMode.Overlay },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultDarkenPixelBlender<RgbaVector>), PixelBlenderMode.Darken },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultLightenPixelBlender<RgbaVector>), PixelBlenderMode.Lighten },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultAddPixelBlender<RgbaVector>), PixelBlenderMode.Add },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultSubstractPixelBlender<RgbaVector>), PixelBlenderMode.Substract },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultMultiplyPixelBlender<RgbaVector>), PixelBlenderMode.Multiply },
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
