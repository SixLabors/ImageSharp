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
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Normal), PixelBlenderMode.Normal },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Screen), PixelBlenderMode.Screen },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.HardLight), PixelBlenderMode.HardLight },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Overlay), PixelBlenderMode.Overlay },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Darken), PixelBlenderMode.Darken },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Lighten), PixelBlenderMode.Lighten },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Add), PixelBlenderMode.Add },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Substract), PixelBlenderMode.Substract },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Multiply), PixelBlenderMode.Multiply },

                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Src), PixelBlenderMode.Src },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Atop), PixelBlenderMode.Atop },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Over), PixelBlenderMode.Over },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.In), PixelBlenderMode.In },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Out), PixelBlenderMode.Out },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Dest), PixelBlenderMode.Dest },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.DestAtop), PixelBlenderMode.DestAtop },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.DestOver), PixelBlenderMode.DestOver },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.DestIn), PixelBlenderMode.DestIn },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.DestOut), PixelBlenderMode.DestOut },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Clear), PixelBlenderMode.Clear },
                                                                                           { new TestPixel<Rgba32>(), typeof(DefaultPixelBlenders<Rgba32>.Xor), PixelBlenderMode.Xor },

                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Normal), PixelBlenderMode.Normal },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Screen), PixelBlenderMode.Screen },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.HardLight), PixelBlenderMode.HardLight },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Overlay), PixelBlenderMode.Overlay },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Darken), PixelBlenderMode.Darken },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Lighten), PixelBlenderMode.Lighten },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Add), PixelBlenderMode.Add },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Substract), PixelBlenderMode.Substract },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Multiply), PixelBlenderMode.Multiply },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Src), PixelBlenderMode.Src },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Atop), PixelBlenderMode.Atop },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Over), PixelBlenderMode.Over },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.In), PixelBlenderMode.In },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Out), PixelBlenderMode.Out },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Dest), PixelBlenderMode.Dest },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.DestAtop), PixelBlenderMode.DestAtop },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.DestOver), PixelBlenderMode.DestOver },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.DestIn), PixelBlenderMode.DestIn },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.DestOut), PixelBlenderMode.DestOut },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Clear), PixelBlenderMode.Clear },
                                                                                           { new TestPixel<RgbaVector>(), typeof(DefaultPixelBlenders<RgbaVector>.Xor), PixelBlenderMode.Xor },

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
