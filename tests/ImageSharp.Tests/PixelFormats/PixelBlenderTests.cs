// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class PixelBlenderTests
{
    public static TheoryData<object, Type, PixelColorBlendingMode> BlenderMappings = new()
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

    public static TheoryData<PixelColorBlendingMode, PixelAlphaCompositionMode, Type> BlenderModeMappings
    {
        get
        {
            TheoryData<PixelColorBlendingMode, PixelAlphaCompositionMode, Type> data = new();

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.Clear,
                typeof(DefaultPixelBlenders<Rgba32>.NormalClear),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplyClear),
                typeof(DefaultPixelBlenders<Rgba32>.AddClear),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractClear),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenClear),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenClear),
                typeof(DefaultPixelBlenders<Rgba32>.LightenClear),
                typeof(DefaultPixelBlenders<Rgba32>.OverlayClear),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightClear));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.Xor,
                typeof(DefaultPixelBlenders<Rgba32>.NormalXor),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplyXor),
                typeof(DefaultPixelBlenders<Rgba32>.AddXor),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractXor),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenXor),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenXor),
                typeof(DefaultPixelBlenders<Rgba32>.LightenXor),
                typeof(DefaultPixelBlenders<Rgba32>.OverlayXor),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightXor));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.Src,
                typeof(DefaultPixelBlenders<Rgba32>.NormalSrc),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplySrc),
                typeof(DefaultPixelBlenders<Rgba32>.AddSrc),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractSrc),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenSrc),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenSrc),
                typeof(DefaultPixelBlenders<Rgba32>.LightenSrc),
                typeof(DefaultPixelBlenders<Rgba32>.OverlaySrc),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightSrc));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.SrcAtop,
                typeof(DefaultPixelBlenders<Rgba32>.NormalSrcAtop),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplySrcAtop),
                typeof(DefaultPixelBlenders<Rgba32>.AddSrcAtop),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractSrcAtop),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenSrcAtop),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenSrcAtop),
                typeof(DefaultPixelBlenders<Rgba32>.LightenSrcAtop),
                typeof(DefaultPixelBlenders<Rgba32>.OverlaySrcAtop),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightSrcAtop));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.SrcIn,
                typeof(DefaultPixelBlenders<Rgba32>.NormalSrcIn),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplySrcIn),
                typeof(DefaultPixelBlenders<Rgba32>.AddSrcIn),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractSrcIn),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenSrcIn),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenSrcIn),
                typeof(DefaultPixelBlenders<Rgba32>.LightenSrcIn),
                typeof(DefaultPixelBlenders<Rgba32>.OverlaySrcIn),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightSrcIn));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.SrcOut,
                typeof(DefaultPixelBlenders<Rgba32>.NormalSrcOut),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplySrcOut),
                typeof(DefaultPixelBlenders<Rgba32>.AddSrcOut),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractSrcOut),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenSrcOut),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenSrcOut),
                typeof(DefaultPixelBlenders<Rgba32>.LightenSrcOut),
                typeof(DefaultPixelBlenders<Rgba32>.OverlaySrcOut),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightSrcOut));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.Dest,
                typeof(DefaultPixelBlenders<Rgba32>.NormalDest),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplyDest),
                typeof(DefaultPixelBlenders<Rgba32>.AddDest),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractDest),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenDest),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenDest),
                typeof(DefaultPixelBlenders<Rgba32>.LightenDest),
                typeof(DefaultPixelBlenders<Rgba32>.OverlayDest),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightDest));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.DestAtop,
                typeof(DefaultPixelBlenders<Rgba32>.NormalDestAtop),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplyDestAtop),
                typeof(DefaultPixelBlenders<Rgba32>.AddDestAtop),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractDestAtop),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenDestAtop),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenDestAtop),
                typeof(DefaultPixelBlenders<Rgba32>.LightenDestAtop),
                typeof(DefaultPixelBlenders<Rgba32>.OverlayDestAtop),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightDestAtop));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.DestIn,
                typeof(DefaultPixelBlenders<Rgba32>.NormalDestIn),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplyDestIn),
                typeof(DefaultPixelBlenders<Rgba32>.AddDestIn),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractDestIn),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenDestIn),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenDestIn),
                typeof(DefaultPixelBlenders<Rgba32>.LightenDestIn),
                typeof(DefaultPixelBlenders<Rgba32>.OverlayDestIn),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightDestIn));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.DestOut,
                typeof(DefaultPixelBlenders<Rgba32>.NormalDestOut),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplyDestOut),
                typeof(DefaultPixelBlenders<Rgba32>.AddDestOut),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractDestOut),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenDestOut),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenDestOut),
                typeof(DefaultPixelBlenders<Rgba32>.LightenDestOut),
                typeof(DefaultPixelBlenders<Rgba32>.OverlayDestOut),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightDestOut));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.DestOver,
                typeof(DefaultPixelBlenders<Rgba32>.NormalDestOver),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplyDestOver),
                typeof(DefaultPixelBlenders<Rgba32>.AddDestOver),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractDestOver),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenDestOver),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenDestOver),
                typeof(DefaultPixelBlenders<Rgba32>.LightenDestOver),
                typeof(DefaultPixelBlenders<Rgba32>.OverlayDestOver),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightDestOver));

            AddBlenderModeMappings(
                data,
                PixelAlphaCompositionMode.SrcOver,
                typeof(DefaultPixelBlenders<Rgba32>.NormalSrcOver),
                typeof(DefaultPixelBlenders<Rgba32>.MultiplySrcOver),
                typeof(DefaultPixelBlenders<Rgba32>.AddSrcOver),
                typeof(DefaultPixelBlenders<Rgba32>.SubtractSrcOver),
                typeof(DefaultPixelBlenders<Rgba32>.ScreenSrcOver),
                typeof(DefaultPixelBlenders<Rgba32>.DarkenSrcOver),
                typeof(DefaultPixelBlenders<Rgba32>.LightenSrcOver),
                typeof(DefaultPixelBlenders<Rgba32>.OverlaySrcOver),
                typeof(DefaultPixelBlenders<Rgba32>.HardLightSrcOver));

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(BlenderMappings))]
    public void ReturnsCorrectBlender<TPixel>(TestPixel<TPixel> pixel, Type type, PixelColorBlendingMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(mode, PixelAlphaCompositionMode.SrcOver);
        Assert.IsType(type, blender);
    }

    [Theory]
    [MemberData(nameof(BlenderModeMappings))]
    public void ReturnsCorrectBlenderForAllModeCombinations(PixelColorBlendingMode colorMode, PixelAlphaCompositionMode alphaMode, Type type)
    {
        PixelBlender<Rgba32> blender = PixelOperations<Rgba32>.Instance.GetPixelBlender(colorMode, alphaMode);
        Assert.IsType(type, blender);
    }

    [Fact]
    public void BlendFunctionsAreCalledForAllModeCombinations() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ExerciseAllBlenderModeCombinations,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void Blend_WithConstantSourceAndSingleAmount()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrcOver();
        Rgba32[] destination = new Rgba32[2];
        Rgba32[] background =
        [
            Color.Red.ToPixel<Rgba32>(),
            Color.Green.ToPixel<Rgba32>()
        ];

        Rgba32 source = Color.Blue.ToPixel<Rgba32>();

        blender.Blend(Configuration.Default, destination, background, source, 1F);

        Assert.Equal(source, destination[0]);
        Assert.Equal(source, destination[1]);
    }

    [Fact]
    public void Blend_WithConstantSourceSingleAmountAndWorkingBuffer()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrcOver();
        Rgba32[] destination = new Rgba32[2];
        Rgba32[] background =
        [
            Color.Red.ToPixel<Rgba32>(),
            Color.Green.ToPixel<Rgba32>()
        ];

        Rgba32 source = Color.Blue.ToPixel<Rgba32>();
        Vector4[] workingBuffer = new Vector4[destination.Length * 2];

        blender.Blend(Configuration.Default, destination, background, source, 1F, workingBuffer);

        Assert.Equal(source, destination[0]);
        Assert.Equal(source, destination[1]);
    }

    [Fact]
    public void Blend_WithConstantSourceAndAmountSpan()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrcOver();
        Rgba32[] destination = new Rgba32[2];
        Rgba32[] background =
        [
            Color.Red.ToPixel<Rgba32>(),
            Color.Green.ToPixel<Rgba32>()
        ];

        Rgba32 source = Color.Blue.ToPixel<Rgba32>();
        float[] amount = [1F, 1F];

        blender.Blend(Configuration.Default, destination, background, source, amount);

        Assert.Equal(source, destination[0]);
        Assert.Equal(source, destination[1]);
    }

    [Fact]
    public void Blend_WithConstantSourceAmountSpanAndWorkingBuffer()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrcOver();
        Rgba32[] destination = new Rgba32[2];
        Rgba32[] background =
        [
            Color.Red.ToPixel<Rgba32>(),
            Color.Green.ToPixel<Rgba32>()
        ];

        Rgba32 source = Color.Blue.ToPixel<Rgba32>();
        float[] amount = [1F, 1F];
        Vector4[] workingBuffer = new Vector4[destination.Length * 2];

        blender.Blend(Configuration.Default, destination, background, source, amount, workingBuffer);

        Assert.Equal(source, destination[0]);
        Assert.Equal(source, destination[1]);
    }

    [Fact]
    public void Blend_WithSourceSpanAmountSpanAndWorkingBuffer()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrcOver();
        Rgba32[] destination = new Rgba32[2];
        Rgba32[] background =
        [
            Color.Red.ToPixel<Rgba32>(),
            Color.Green.ToPixel<Rgba32>()
        ];

        Rgba32[] source =
        [
            Color.Blue.ToPixel<Rgba32>(),
            Color.Yellow.ToPixel<Rgba32>()
        ];

        float[] amount = [1F, 1F];
        Vector4[] workingBuffer = new Vector4[destination.Length * 3];

        blender.Blend(Configuration.Default, destination, background, source, amount, workingBuffer);

        Assert.Equal(source[0], destination[0]);
        Assert.Equal(source[1], destination[1]);
    }

    public static TheoryData<Rgba32, Rgba32, float, PixelColorBlendingMode, Rgba32> ColorBlendingExpectedResults = new()
    {
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.Normal, Color.MidnightBlue.ToPixel<Rgba32>() },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.Screen, new Rgba32(0xFFEEE7FF) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.HardLight, new Rgba32(0xFFC62D32) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.Overlay, new Rgba32(0xFFDDCEFF) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.Darken, new Rgba32(0xFF701919) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.Lighten, new Rgba32(0xFFE1E4FF) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.Add, new Rgba32(0xFFFFFDFF) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.Subtract, new Rgba32(0xFF71CBE6) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelColorBlendingMode.Multiply, new Rgba32(0xFF631619) },
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

    public static TheoryData<Rgba32, Rgba32, float, PixelAlphaCompositionMode, Rgba32> AlphaCompositionExpectedResults = new()
    {
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.Clear, new Rgba32(0) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.Xor, new Rgba32(0) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.Dest, Color.MistyRose.ToPixel<Rgba32>() },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.DestAtop, Color.MistyRose.ToPixel<Rgba32>() },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.DestIn, Color.MistyRose.ToPixel<Rgba32>() },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.DestOut, new Rgba32(0) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.DestOver, Color.MistyRose.ToPixel<Rgba32>() },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.Src, Color.MidnightBlue.ToPixel<Rgba32>() },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.SrcAtop, Color.MidnightBlue.ToPixel<Rgba32>() },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.SrcIn, Color.MidnightBlue.ToPixel<Rgba32>() },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.SrcOut, new Rgba32(0) },
        { Color.MistyRose.ToPixel<Rgba32>(), Color.MidnightBlue.ToPixel<Rgba32>(), 1, PixelAlphaCompositionMode.SrcOver, Color.MidnightBlue.ToPixel<Rgba32>() },
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

    private static void AddBlenderModeMappings(
        TheoryData<PixelColorBlendingMode, PixelAlphaCompositionMode, Type> data,
        PixelAlphaCompositionMode alphaMode,
        Type normal,
        Type multiply,
        Type add,
        Type subtract,
        Type screen,
        Type darken,
        Type lighten,
        Type overlay,
        Type hardLight)
    {
        data.Add(PixelColorBlendingMode.Normal, alphaMode, normal);
        data.Add(PixelColorBlendingMode.Multiply, alphaMode, multiply);
        data.Add(PixelColorBlendingMode.Add, alphaMode, add);
        data.Add(PixelColorBlendingMode.Subtract, alphaMode, subtract);
        data.Add(PixelColorBlendingMode.Screen, alphaMode, screen);
        data.Add(PixelColorBlendingMode.Darken, alphaMode, darken);
        data.Add(PixelColorBlendingMode.Lighten, alphaMode, lighten);
        data.Add(PixelColorBlendingMode.Overlay, alphaMode, overlay);
        data.Add(PixelColorBlendingMode.HardLight, alphaMode, hardLight);
    }

    private static void ExerciseAllBlenderModeCombinations()
    {
        foreach (PixelAlphaCompositionMode alphaMode in Enum.GetValues<PixelAlphaCompositionMode>())
        {
            foreach (PixelColorBlendingMode colorMode in Enum.GetValues<PixelColorBlendingMode>())
            {
                PixelBlender<Rgba32> blender = PixelOperations<Rgba32>.Instance.GetPixelBlender(colorMode, alphaMode);
                ExerciseBlender(blender);
            }
        }
    }

    private static void ExerciseBlender(PixelBlender<Rgba32> blender)
    {
        Rgba32 background = Color.MistyRose.ToPixel<Rgba32>();
        Rgba32 source = Color.MidnightBlue.ToPixel<Rgba32>();
        float[] amount = [1F, 1F, 1F, 1F];

        Rgba32 expected = blender.Blend(background, source, 1F);

        Rgba32[] destination = new Rgba32[4];
        Rgba32[] backgroundSpan = [background, background, background, background];
        Rgba32[] sourceSpan = [source, source, source, source];
        Vector4[] sourceSpanBuffer = new Vector4[destination.Length * 3];
        Vector4[] constantSourceBuffer = new Vector4[destination.Length * 2];

        blender.Blend<Rgba32>(Configuration.Default, destination, backgroundSpan, sourceSpan, 1F, sourceSpanBuffer);
        Assert.All(destination, x => Assert.Equal(expected, x));

        blender.Blend(Configuration.Default, destination, backgroundSpan, source, 1F, constantSourceBuffer);
        Assert.All(destination, x => Assert.Equal(expected, x));

        blender.Blend(Configuration.Default, destination, backgroundSpan, sourceSpan, amount, sourceSpanBuffer);
        Assert.All(destination, x => Assert.Equal(expected, x));

        blender.Blend(Configuration.Default, destination, backgroundSpan, source, amount, constantSourceBuffer);
        Assert.All(destination, x => Assert.Equal(expected, x));
    }
}
