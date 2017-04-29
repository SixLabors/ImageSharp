

namespace ImageSharp.Tests.PixelFormats
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using ImageSharp.PixelFormats;
    using ImageSharp.PixelFormats.PixelBlenders;
    using ImageSharp.Tests.TestUtilities;
    using Xunit;

    public class PixelOperations
    {
        public static TheoryData<object, Type, PixelBlenderMode> blenderMappings = new TheoryData<object, Type, PixelBlenderMode>()
        {
            { new TestPixel<Rgba32>(), typeof(DefaultPremultipliedLerpPixelBlender<Rgba32>), PixelBlenderMode.Default },
            { new TestPixel<Rgba32>(), typeof(DefaultNormalPixelBlender<Rgba32>), PixelBlenderMode.Normal },
            { new TestPixel<Rgba32>(), typeof(DefaultScreenPixelBlender<Rgba32>), PixelBlenderMode.Screen },
            { new TestPixel<Rgba32>(), typeof(DefaultHardLightPixelBlender<Rgba32>), PixelBlenderMode.HardLight },
            { new TestPixel<Rgba32>(), typeof(DefaultOverlayPixelBlender<Rgba32>), PixelBlenderMode.Overlay },
            { new TestPixel<Rgba32>(), typeof(DefaultDarkenPixelBlender<Rgba32>), PixelBlenderMode.Darken },
            { new TestPixel<Rgba32>(), typeof(DefaultLightenPixelBlender<Rgba32>), PixelBlenderMode.Lighten },
            { new TestPixel<Rgba32>(), typeof(DefaultSoftLightPixelBlender<Rgba32>), PixelBlenderMode.SoftLight },
            { new TestPixel<Rgba32>(), typeof(DefaultDodgePixelBlender<Rgba32>), PixelBlenderMode.Dodge },
            { new TestPixel<Rgba32>(), typeof(DefaultBurnPixelBlender<Rgba32>), PixelBlenderMode.Burn },
            { new TestPixel<Rgba32>(), typeof(DefaultDifferencePixelBlender<Rgba32>), PixelBlenderMode.Difference },
            { new TestPixel<Rgba32>(), typeof(DefaultExclusionPixelBlender<Rgba32>), PixelBlenderMode.Exclusion },

            { new TestPixel<RgbaVector>(), typeof(DefaultPremultipliedLerpPixelBlender<RgbaVector>), PixelBlenderMode.Default },
            { new TestPixel<RgbaVector>(), typeof(DefaultNormalPixelBlender<RgbaVector>), PixelBlenderMode.Normal },
            { new TestPixel<RgbaVector>(), typeof(DefaultScreenPixelBlender<RgbaVector>), PixelBlenderMode.Screen },
            { new TestPixel<RgbaVector>(), typeof(DefaultHardLightPixelBlender<RgbaVector>), PixelBlenderMode.HardLight },
            { new TestPixel<RgbaVector>(), typeof(DefaultOverlayPixelBlender<RgbaVector>), PixelBlenderMode.Overlay },
            { new TestPixel<RgbaVector>(), typeof(DefaultDarkenPixelBlender<RgbaVector>), PixelBlenderMode.Darken },
            { new TestPixel<RgbaVector>(), typeof(DefaultLightenPixelBlender<RgbaVector>), PixelBlenderMode.Lighten },
            { new TestPixel<RgbaVector>(), typeof(DefaultSoftLightPixelBlender<RgbaVector>), PixelBlenderMode.SoftLight },
            { new TestPixel<RgbaVector>(), typeof(DefaultDodgePixelBlender<RgbaVector>), PixelBlenderMode.Dodge },
            { new TestPixel<RgbaVector>(), typeof(DefaultBurnPixelBlender<RgbaVector>), PixelBlenderMode.Burn },
            { new TestPixel<RgbaVector>(), typeof(DefaultDifferencePixelBlender<RgbaVector>), PixelBlenderMode.Difference },
            { new TestPixel<RgbaVector>(), typeof(DefaultExclusionPixelBlender<RgbaVector>), PixelBlenderMode.Exclusion }
        };

        [Theory]
        [MemberData(nameof(blenderMappings))]
        public void ReturnsCorrectBlender<TPixel>(TestPixel<TPixel> pixel, Type type, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(mode);
            Assert.IsType(type, blender);
        }
    }
}
