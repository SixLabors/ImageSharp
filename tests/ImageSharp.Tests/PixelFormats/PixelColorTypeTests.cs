// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

public class PixelColorTypeTests
{
    [Theory]
    [InlineData(PixelColorType.XYZ, IccColorSpaceType.CieXyz)]
    [InlineData(PixelColorType.Lab, IccColorSpaceType.CieLab)]
    [InlineData(PixelColorType.Luv, IccColorSpaceType.CieLuv)]
    [InlineData(PixelColorType.Yxy, IccColorSpaceType.CieYxy)]
    [InlineData(PixelColorType.HSV, IccColorSpaceType.Hsv)]
    [InlineData(PixelColorType.HLS, IccColorSpaceType.Hls)]
    [InlineData(PixelColorType.CMY, IccColorSpaceType.Cmy)]
    [InlineData(PixelColorType.YCbCr, IccColorSpaceType.YCbCr)]
    public void NamedColorType_MapsToIccDataSpaceWithOrWithoutAlpha(PixelColorType colorType, IccColorSpaceType dataSpace)
    {
        Assert.True(PixelColorTypeIccMapping.TryGetDataSpace(colorType, out IccColorSpaceType directSpace));
        Assert.Equal(dataSpace, directSpace);
        Assert.True(PixelColorTypeIccMapping.TryGetDataSpace(colorType | PixelColorType.Alpha, out IccColorSpaceType actualSpace));
        Assert.Equal(dataSpace, actualSpace);

        Assert.True(PixelColorTypeIccMapping.TryGetColorType(dataSpace, out PixelColorType actualType));
        Assert.Equal(colorType, actualType);
    }

    [Fact]
    public void IndexedHasNoDirectIccDataSpaceAndYcckUsesCmyk()
    {
        Assert.False(PixelColorTypeIccMapping.TryGetDataSpace(PixelColorType.Indexed, out _));
        Assert.True(PixelColorTypeIccMapping.TryGetDataSpace(PixelColorType.YCCK, out IccColorSpaceType dataSpace));
        Assert.Equal(IccColorSpaceType.Cmyk, dataSpace);
    }

    [Fact]
    public void NamedColorTypesShareOnlyTheirCommonComponents()
    {
        Assert.Equal(PixelColorType.Luminance, PixelColorType.XYZ & PixelColorType.Yxy);
        Assert.Equal(PixelColorType.Luminance, PixelColorType.Lab & PixelColorType.Luv);
        Assert.Equal(PixelColorType.Hue | PixelColorType.Saturation | PixelColorType.Luminance, PixelColorType.HSV & PixelColorType.HLS);
        Assert.False(PixelColorType.HSV.HasFlag(PixelColorType.HLS));
        Assert.False(PixelColorType.HLS.HasFlag(PixelColorType.HSV));
    }

    [Fact]
    public void CmyComposesExistingInkFlags()
    {
        Assert.Equal(PixelColorType.Cyan | PixelColorType.Magenta | PixelColorType.Yellow, PixelColorType.CMY);
    }

    [Fact]
    public void PixelColorType_RedFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Red;
        Assert.True(colorType.HasFlag(PixelColorType.Red));
    }

    [Fact]
    public void PixelColorType_GreenFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Green;
        Assert.True(colorType.HasFlag(PixelColorType.Green));
    }

    [Fact]
    public void PixelColorType_BlueFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Blue;
        Assert.True(colorType.HasFlag(PixelColorType.Blue));
    }

    [Fact]
    public void PixelColorType_AlphaFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Alpha;
        Assert.True(colorType.HasFlag(PixelColorType.Alpha));
    }

    [Fact]
    public void PixelColorType_Exponent_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Exponent;
        Assert.True(colorType.HasFlag(PixelColorType.Exponent));
    }

    [Fact]
    public void PixelColorType_LuminanceFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Luminance;
        Assert.True(colorType.HasFlag(PixelColorType.Luminance));
    }

    [Fact]
    public void PixelColorType_Binary_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Binary;
        Assert.True(colorType.HasFlag(PixelColorType.Binary));
    }

    [Fact]
    public void PixelColorType_Indexed_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Indexed;
        Assert.True(colorType.HasFlag(PixelColorType.Indexed));
    }

    [Fact]
    public void PixelColorType_RGBFlags_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.RGB;
        Assert.True(colorType.HasFlag(PixelColorType.Red));
        Assert.True(colorType.HasFlag(PixelColorType.Green));
        Assert.True(colorType.HasFlag(PixelColorType.Blue));
        Assert.False(colorType.HasFlag(PixelColorType.BGR));
    }

    [Fact]
    public void PixelColorType_BGRFlags_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.BGR;
        Assert.True(colorType.HasFlag(PixelColorType.Blue));
        Assert.True(colorType.HasFlag(PixelColorType.Green));
        Assert.True(colorType.HasFlag(PixelColorType.Red));
        Assert.False(colorType.HasFlag(PixelColorType.RGB));
    }

    [Fact]
    public void PixelColorType_ChrominanceBlueFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.ChrominanceBlue;
        Assert.True(colorType.HasFlag(PixelColorType.ChrominanceBlue));
    }

    [Fact]
    public void PixelColorType_ChrominanceRedFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.ChrominanceRed;
        Assert.True(colorType.HasFlag(PixelColorType.ChrominanceRed));
    }

    [Fact]
    public void PixelColorType_YCbCrFlags_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.YCbCr;
        Assert.True(colorType.HasFlag(PixelColorType.Luminance));
        Assert.True(colorType.HasFlag(PixelColorType.ChrominanceBlue));
        Assert.True(colorType.HasFlag(PixelColorType.ChrominanceRed));
    }

    [Fact]
    public void PixelColorType_CyanFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Cyan;
        Assert.True(colorType.HasFlag(PixelColorType.Cyan));
    }

    [Fact]
    public void PixelColorType_MagentaFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Magenta;
        Assert.True(colorType.HasFlag(PixelColorType.Magenta));
    }

    [Fact]
    public void PixelColorType_YellowFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Yellow;
        Assert.True(colorType.HasFlag(PixelColorType.Yellow));
    }

    [Fact]
    public void PixelColorType_KeyFlag_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Key;
        Assert.True(colorType.HasFlag(PixelColorType.Key));
    }

    [Fact]
    public void PixelColorType_CMYKFlags_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.CMYK;
        Assert.True(colorType.HasFlag(PixelColorType.Cyan));
        Assert.True(colorType.HasFlag(PixelColorType.Magenta));
        Assert.True(colorType.HasFlag(PixelColorType.Yellow));
        Assert.True(colorType.HasFlag(PixelColorType.Key));
    }

    [Fact]
    public void PixelColorType_YCCKFlags_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.YCCK;
        Assert.True(colorType.HasFlag(PixelColorType.Luminance));
        Assert.True(colorType.HasFlag(PixelColorType.ChrominanceBlue));
        Assert.True(colorType.HasFlag(PixelColorType.ChrominanceRed));
        Assert.True(colorType.HasFlag(PixelColorType.Key));
    }

    [Fact]
    public void PixelColorType_Other_ShouldBeSet()
    {
        const PixelColorType colorType = PixelColorType.Other;
        Assert.True(colorType.HasFlag(PixelColorType.Other));
    }

    [Fact]
    public void PixelColorType_None_ShouldBeZero()
    {
        const PixelColorType colorType = PixelColorType.None;
        Assert.Equal(0, (int)colorType);
    }

    [Fact]
    public void PixelColorType_RGB_ShouldNotContainOtherFlags()
    {
        const PixelColorType colorType = PixelColorType.RGB;
        Assert.False(colorType.HasFlag(PixelColorType.Alpha));
        Assert.False(colorType.HasFlag(PixelColorType.Exponent));
        Assert.False(colorType.HasFlag(PixelColorType.Luminance));
        Assert.False(colorType.HasFlag(PixelColorType.Binary));
        Assert.False(colorType.HasFlag(PixelColorType.Indexed));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceBlue));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceRed));
        Assert.False(colorType.HasFlag(PixelColorType.Cyan));
        Assert.False(colorType.HasFlag(PixelColorType.Magenta));
        Assert.False(colorType.HasFlag(PixelColorType.Yellow));
        Assert.False(colorType.HasFlag(PixelColorType.Key));
        Assert.False(colorType.HasFlag(PixelColorType.Other));
    }

    [Fact]
    public void PixelColorType_BGR_ShouldNotContainOtherFlags()
    {
        const PixelColorType colorType = PixelColorType.BGR;
        Assert.False(colorType.HasFlag(PixelColorType.Alpha));
        Assert.False(colorType.HasFlag(PixelColorType.Exponent));
        Assert.False(colorType.HasFlag(PixelColorType.Luminance));
        Assert.False(colorType.HasFlag(PixelColorType.Binary));
        Assert.False(colorType.HasFlag(PixelColorType.Indexed));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceBlue));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceRed));
        Assert.False(colorType.HasFlag(PixelColorType.Cyan));
        Assert.False(colorType.HasFlag(PixelColorType.Magenta));
        Assert.False(colorType.HasFlag(PixelColorType.Yellow));
        Assert.False(colorType.HasFlag(PixelColorType.Key));
        Assert.False(colorType.HasFlag(PixelColorType.Other));
    }

    [Fact]
    public void PixelColorType_YCbCr_ShouldNotContainOtherFlags()
    {
        const PixelColorType colorType = PixelColorType.YCbCr;
        Assert.False(colorType.HasFlag(PixelColorType.Red));
        Assert.False(colorType.HasFlag(PixelColorType.Green));
        Assert.False(colorType.HasFlag(PixelColorType.Blue));
        Assert.False(colorType.HasFlag(PixelColorType.Alpha));
        Assert.False(colorType.HasFlag(PixelColorType.Exponent));
        Assert.False(colorType.HasFlag(PixelColorType.Binary));
        Assert.False(colorType.HasFlag(PixelColorType.Indexed));
        Assert.False(colorType.HasFlag(PixelColorType.Cyan));
        Assert.False(colorType.HasFlag(PixelColorType.Magenta));
        Assert.False(colorType.HasFlag(PixelColorType.Yellow));
        Assert.False(colorType.HasFlag(PixelColorType.Key));
        Assert.False(colorType.HasFlag(PixelColorType.Other));
    }

    [Fact]
    public void PixelColorType_CMYK_ShouldNotContainOtherFlags()
    {
        const PixelColorType colorType = PixelColorType.CMYK;
        Assert.False(colorType.HasFlag(PixelColorType.Red));
        Assert.False(colorType.HasFlag(PixelColorType.Green));
        Assert.False(colorType.HasFlag(PixelColorType.Blue));
        Assert.False(colorType.HasFlag(PixelColorType.Alpha));
        Assert.False(colorType.HasFlag(PixelColorType.Exponent));
        Assert.False(colorType.HasFlag(PixelColorType.Luminance));
        Assert.False(colorType.HasFlag(PixelColorType.Binary));
        Assert.False(colorType.HasFlag(PixelColorType.Indexed));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceBlue));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceRed));
        Assert.False(colorType.HasFlag(PixelColorType.Other));
    }

    [Fact]
    public void PixelColorType_YCCK_ShouldNotContainOtherFlags()
    {
        const PixelColorType colorType = PixelColorType.YCCK;
        Assert.False(colorType.HasFlag(PixelColorType.Red));
        Assert.False(colorType.HasFlag(PixelColorType.Green));
        Assert.False(colorType.HasFlag(PixelColorType.Blue));
        Assert.False(colorType.HasFlag(PixelColorType.Alpha));
        Assert.False(colorType.HasFlag(PixelColorType.Exponent));
        Assert.False(colorType.HasFlag(PixelColorType.Binary));
        Assert.False(colorType.HasFlag(PixelColorType.Indexed));
        Assert.False(colorType.HasFlag(PixelColorType.Cyan));
        Assert.False(colorType.HasFlag(PixelColorType.Magenta));
        Assert.False(colorType.HasFlag(PixelColorType.Yellow));
        Assert.False(colorType.HasFlag(PixelColorType.Other));
    }

    [Fact]
    public void PixelColorType_Binary_ShouldNotContainOtherFlags()
    {
        const PixelColorType colorType = PixelColorType.Binary;
        Assert.False(colorType.HasFlag(PixelColorType.Red));
        Assert.False(colorType.HasFlag(PixelColorType.Green));
        Assert.False(colorType.HasFlag(PixelColorType.Blue));
        Assert.False(colorType.HasFlag(PixelColorType.Alpha));
        Assert.False(colorType.HasFlag(PixelColorType.Exponent));
        Assert.False(colorType.HasFlag(PixelColorType.Luminance));
        Assert.False(colorType.HasFlag(PixelColorType.Indexed));
        Assert.False(colorType.HasFlag(PixelColorType.RGB));
        Assert.False(colorType.HasFlag(PixelColorType.BGR));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceBlue));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceRed));
        Assert.False(colorType.HasFlag(PixelColorType.YCbCr));
        Assert.False(colorType.HasFlag(PixelColorType.Cyan));
        Assert.False(colorType.HasFlag(PixelColorType.Magenta));
        Assert.False(colorType.HasFlag(PixelColorType.Yellow));
        Assert.False(colorType.HasFlag(PixelColorType.Key));
        Assert.False(colorType.HasFlag(PixelColorType.CMYK));
        Assert.False(colorType.HasFlag(PixelColorType.YCCK));
        Assert.False(colorType.HasFlag(PixelColorType.Other));
    }

    [Fact]
    public void PixelColorType_Indexed_ShouldNotContainOtherFlags()
    {
        const PixelColorType colorType = PixelColorType.Indexed;
        Assert.False(colorType.HasFlag(PixelColorType.Red));
        Assert.False(colorType.HasFlag(PixelColorType.Green));
        Assert.False(colorType.HasFlag(PixelColorType.Blue));
        Assert.False(colorType.HasFlag(PixelColorType.Alpha));
        Assert.False(colorType.HasFlag(PixelColorType.Exponent));
        Assert.False(colorType.HasFlag(PixelColorType.Luminance));
        Assert.False(colorType.HasFlag(PixelColorType.Binary));
        Assert.False(colorType.HasFlag(PixelColorType.RGB));
        Assert.False(colorType.HasFlag(PixelColorType.BGR));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceBlue));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceRed));
        Assert.False(colorType.HasFlag(PixelColorType.YCbCr));
        Assert.False(colorType.HasFlag(PixelColorType.Cyan));
        Assert.False(colorType.HasFlag(PixelColorType.Magenta));
        Assert.False(colorType.HasFlag(PixelColorType.Yellow));
        Assert.False(colorType.HasFlag(PixelColorType.Key));
        Assert.False(colorType.HasFlag(PixelColorType.CMYK));
        Assert.False(colorType.HasFlag(PixelColorType.YCCK));
        Assert.False(colorType.HasFlag(PixelColorType.Other));
    }

    [Fact]
    public void PixelColorType_Other_ShouldNotContainPreviousFlags()
    {
        const PixelColorType colorType = PixelColorType.Other;
        Assert.False(colorType.HasFlag(PixelColorType.Red));
        Assert.False(colorType.HasFlag(PixelColorType.Green));
        Assert.False(colorType.HasFlag(PixelColorType.Blue));
        Assert.False(colorType.HasFlag(PixelColorType.Alpha));
        Assert.False(colorType.HasFlag(PixelColorType.Exponent));
        Assert.False(colorType.HasFlag(PixelColorType.Luminance));
        Assert.False(colorType.HasFlag(PixelColorType.Binary));
        Assert.False(colorType.HasFlag(PixelColorType.Indexed));
        Assert.False(colorType.HasFlag(PixelColorType.RGB));
        Assert.False(colorType.HasFlag(PixelColorType.BGR));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceBlue));
        Assert.False(colorType.HasFlag(PixelColorType.ChrominanceRed));
        Assert.False(colorType.HasFlag(PixelColorType.YCbCr));
        Assert.False(colorType.HasFlag(PixelColorType.Cyan));
        Assert.False(colorType.HasFlag(PixelColorType.Magenta));
        Assert.False(colorType.HasFlag(PixelColorType.Yellow));
        Assert.False(colorType.HasFlag(PixelColorType.Key));
        Assert.False(colorType.HasFlag(PixelColorType.CMYK));
        Assert.False(colorType.HasFlag(PixelColorType.YCCK));
    }
}
