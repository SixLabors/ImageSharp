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
    public void AssociatedAlphaBlendFunctionsAreCalledForAllModeCombinations() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ExerciseAllAssociatedAlphaBlenderModeCombinations,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void AssociatedHardLightDestAtopRoundsExactMidpointAwayFromZero() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunAssociatedHardLightDestAtopRoundsExactMidpointAwayFromZero,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    private static void RunAssociatedHardLightDestAtopRoundsExactMidpointAwayFromZero()
    {
        Rgba32P background = Rgba32P.FromRgba32(new Rgba32(220, 80, 40, 160));
        Rgba32P source = Rgba32P.FromRgba32(new Rgba32(20, 180, 120, 96));
        PixelBlender<Rgba32P> blender = PixelOperations<Rgba32P>.Instance.GetPixelBlender(PixelColorBlendingMode.HardLight, PixelAlphaCompositionMode.DestAtop);

        Assert.Equal(new Rgba32P(30, 33, 16, 60), blender.Blend(background, source, .625F));
    }

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

    [Fact]
    public void BlendWithCoverage_WithConstantSourceAndSingleAmount()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrc();
        Rgba32[] destination = new Rgba32[3];
        Rgba32[] background =
        [
            new(255, 0, 0),
            new(255, 0, 0),
            new(255, 0, 0)
        ];

        Rgba32 source = new(0, 0, 255);
        float[] coverage = [0F, .5F, 1F];

        blender.BlendWithCoverage(Configuration.Default, destination, background, source, 1F, coverage);

        Assert.Equal(background[0], destination[0]);
        Assert.Equal(new Rgba32(128, 0, 128), destination[1]);
        Assert.Equal(source, destination[2]);
    }

    [Fact]
    public void BlendWithCoverage_WithConstantSourceSingleAmountAndWorkingBuffer()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrc();
        Rgba32[] destination = new Rgba32[3];
        Rgba32[] background =
        [
            new(255, 0, 0),
            new(255, 0, 0),
            new(255, 0, 0)
        ];

        Rgba32 source = new(0, 0, 255);
        float[] coverage = [0F, .5F, 1F];
        Vector4[] workingBuffer = new Vector4[destination.Length * 2];

        blender.BlendWithCoverage(Configuration.Default, destination, background, source, 1F, coverage, workingBuffer);

        Assert.Equal(background[0], destination[0]);
        Assert.Equal(new Rgba32(128, 0, 128), destination[1]);
        Assert.Equal(source, destination[2]);
    }

    [Fact]
    public void BlendWithCoverage_WithSourceSpanAndSingleAmount()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrc();
        Rgba32[] destination = new Rgba32[3];
        Rgba32[] background =
        [
            new(255, 0, 0),
            new(0, 255, 0),
            new(0, 0, 255)
        ];

        Rgba32[] source =
        [
            new(0, 0, 255),
            new(255, 0, 0),
            new(0, 255, 0)
        ];

        float[] coverage = [0F, .5F, 1F];

        blender.BlendWithCoverage<Rgba32>(Configuration.Default, destination, background, source, 1F, coverage);

        Assert.Equal(background[0], destination[0]);
        Assert.Equal(new Rgba32(128, 128, 0), destination[1]);
        Assert.Equal(source[2], destination[2]);
    }

    [Fact]
    public void BlendWithCoverage_WithSourceSpanSingleAmountAndWorkingBuffer()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrc();
        Rgba32[] destination = new Rgba32[3];
        Rgba32[] background =
        [
            new(255, 0, 0),
            new(0, 255, 0),
            new(0, 0, 255)
        ];

        Rgba32[] source =
        [
            new(0, 0, 255),
            new(255, 0, 0),
            new(0, 255, 0)
        ];

        float[] coverage = [0F, .5F, 1F];
        Vector4[] workingBuffer = new Vector4[destination.Length * 3];

        blender.BlendWithCoverage<Rgba32>(Configuration.Default, destination, background, source, 1F, coverage, workingBuffer);

        Assert.Equal(background[0], destination[0]);
        Assert.Equal(new Rgba32(128, 128, 0), destination[1]);
        Assert.Equal(source[2], destination[2]);
    }

    [Fact]
    public void BlendWithCoverage_WithConstantSourceAndAmountSpan()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrc();
        Rgba32[] destination = new Rgba32[3];
        Rgba32[] background =
        [
            new(255, 0, 0),
            new(255, 0, 0),
            new(255, 0, 0)
        ];

        Rgba32 source = new(0, 0, 255);
        float[] amount = [1F, 1F, 1F];
        float[] coverage = [0F, .5F, 1F];

        blender.BlendWithCoverage(Configuration.Default, destination, background, source, amount, coverage);

        Assert.Equal(background[0], destination[0]);
        Assert.Equal(new Rgba32(128, 0, 128), destination[1]);
        Assert.Equal(source, destination[2]);
    }

    [Fact]
    public void BlendWithCoverage_WithConstantSourceAmountSpanAndWorkingBuffer()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrc();
        Rgba32[] destination = new Rgba32[3];
        Rgba32[] background =
        [
            new(255, 0, 0),
            new(255, 0, 0),
            new(255, 0, 0)
        ];

        Rgba32 source = new(0, 0, 255);
        float[] amount = [1F, 1F, 1F];
        float[] coverage = [0F, .5F, 1F];
        Vector4[] workingBuffer = new Vector4[destination.Length * 2];

        blender.BlendWithCoverage(Configuration.Default, destination, background, source, amount, coverage, workingBuffer);

        Assert.Equal(background[0], destination[0]);
        Assert.Equal(new Rgba32(128, 0, 128), destination[1]);
        Assert.Equal(source, destination[2]);
    }

    [Fact]
    public void BlendWithCoverage_WithSourceSpanAndAmountSpan()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrc();
        Rgba32[] destination = new Rgba32[3];
        Rgba32[] background =
        [
            new(255, 0, 0),
            new(0, 255, 0),
            new(0, 0, 255)
        ];

        Rgba32[] source =
        [
            new(0, 0, 255),
            new(255, 0, 0),
            new(0, 255, 0)
        ];

        float[] amount = [1F, 1F, 1F];
        float[] coverage = [0F, .5F, 1F];

        blender.BlendWithCoverage<Rgba32>(Configuration.Default, destination, background, source, amount, coverage);

        Assert.Equal(background[0], destination[0]);
        Assert.Equal(new Rgba32(128, 128, 0), destination[1]);
        Assert.Equal(source[2], destination[2]);
    }

    [Fact]
    public void BlendWithCoverage_WithSourceSpanAmountSpanAndWorkingBuffer()
    {
        PixelBlender<Rgba32> blender = new DefaultPixelBlenders<Rgba32>.NormalSrc();
        Rgba32[] destination = new Rgba32[3];
        Rgba32[] background =
        [
            new(255, 0, 0),
            new(0, 255, 0),
            new(0, 0, 255)
        ];

        Rgba32[] source =
        [
            new(0, 0, 255),
            new(255, 0, 0),
            new(0, 255, 0)
        ];

        float[] amount = [1F, 1F, 1F];
        float[] coverage = [0F, .5F, 1F];
        Vector4[] workingBuffer = new Vector4[destination.Length * 3];

        blender.BlendWithCoverage<Rgba32>(Configuration.Default, destination, background, source, amount, coverage, workingBuffer);

        Assert.Equal(background[0], destination[0]);
        Assert.Equal(new Rgba32(128, 128, 0), destination[1]);
        Assert.Equal(source[2], destination[2]);
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

    private static void ExerciseAllAssociatedAlphaBlenderModeCombinations()
    {
        Rgba32P[] background =
        [
            Rgba32P.FromRgba32(new Rgba32(220, 80, 40, 160)),
            Rgba32P.FromRgba32(new Rgba32(40, 200, 100, 192)),
            Rgba32P.FromRgba32(new Rgba32(120, 60, 230, 224)),
            Rgba32P.FromRgba32(new Rgba32(180, 160, 20, 128)),
            Rgba32P.FromRgba32(new Rgba32(30, 140, 210, 96)),
        ];

        Rgba32P[] source =
        [
            Rgba32P.FromRgba32(new Rgba32(20, 180, 120, 96)),
            Rgba32P.FromRgba32(new Rgba32(210, 30, 150, 144)),
            Rgba32P.FromRgba32(new Rgba32(80, 220, 40, 176)),
            Rgba32P.FromRgba32(new Rgba32(240, 110, 60, 208)),
            Rgba32P.FromRgba32(new Rgba32(100, 50, 200, 112)),
        ];

        foreach (PixelAlphaCompositionMode alphaMode in Enum.GetValues<PixelAlphaCompositionMode>())
        {
            foreach (PixelColorBlendingMode colorMode in Enum.GetValues<PixelColorBlendingMode>())
            {
                PixelBlender<Rgba32P> blender = PixelOperations<Rgba32P>.Instance.GetPixelBlender(colorMode, alphaMode);
                AssertAssociatedBlenderMatchesScalar(blender, background, source, colorMode, alphaMode);
            }
        }
    }

    private static void AssertAssociatedBlenderMatchesScalar(
        PixelBlender<Rgba32P> blender,
        Rgba32P[] associatedBackground,
        Rgba32P[] associatedSource,
        PixelColorBlendingMode colorMode,
        PixelAlphaCompositionMode alphaMode)
    {
        const float amount = .625F;
        float[] amounts = [.125F, .375F, .625F, .875F, 1F];
        float[] coverage = [.2F, .4F, .6F, .8F, 1F];
        Rgba32P constantAssociatedSource = associatedSource[2];

        Rgba32P[] expected = new Rgba32P[associatedBackground.Length];
        Rgba32P[] actual = new Rgba32P[associatedBackground.Length];
        Vector4[] actualSourceSpanBuffer = new Vector4[associatedBackground.Length * 3];
        Vector4[] actualConstantSourceBuffer = new Vector4[associatedBackground.Length * 2];

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = blender.Blend(associatedBackground[i], associatedSource[i], amount);
        }

        blender.Blend<Rgba32P>(Configuration.Default, actual, associatedBackground, associatedSource, amount, actualSourceSpanBuffer);
        AssertRgba32PEqual(expected, actual, colorMode, alphaMode, "SourceSpanSingleAmount");

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = blender.Blend(associatedBackground[i], constantAssociatedSource, amount);
        }

        blender.Blend(Configuration.Default, actual, associatedBackground, constantAssociatedSource, amount, actualConstantSourceBuffer);
        AssertRgba32PEqual(expected, actual, colorMode, alphaMode, "ConstantSourceSingleAmount");

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = blender.Blend(associatedBackground[i], associatedSource[i], amounts[i]);
        }

        blender.Blend<Rgba32P>(Configuration.Default, actual, associatedBackground, associatedSource, amounts, actualSourceSpanBuffer);
        AssertRgba32PEqual(expected, actual, colorMode, alphaMode, "SourceSpanAmountSpan");

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = blender.Blend(associatedBackground[i], constantAssociatedSource, amounts[i]);
        }

        blender.Blend(Configuration.Default, actual, associatedBackground, constantAssociatedSource, amounts, actualConstantSourceBuffer);
        AssertRgba32PEqual(expected, actual, colorMode, alphaMode, "ConstantSourceAmountSpan");

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = BlendWithCoverageScalar(blender, associatedBackground[i], associatedSource[i], amount, coverage[i]);
        }

        blender.BlendWithCoverage<Rgba32P>(Configuration.Default, actual, associatedBackground, associatedSource, amount, coverage, actualSourceSpanBuffer);
        AssertRgba32PEqual(expected, actual, colorMode, alphaMode, "SourceSpanSingleAmountCoverage");

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = BlendWithCoverageScalar(blender, associatedBackground[i], constantAssociatedSource, amount, coverage[i]);
        }

        blender.BlendWithCoverage(Configuration.Default, actual, associatedBackground, constantAssociatedSource, amount, coverage, actualConstantSourceBuffer);
        AssertRgba32PEqual(expected, actual, colorMode, alphaMode, "ConstantSourceSingleAmountCoverage");

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = BlendWithCoverageScalar(blender, associatedBackground[i], associatedSource[i], amounts[i], coverage[i]);
        }

        blender.BlendWithCoverage<Rgba32P>(Configuration.Default, actual, associatedBackground, associatedSource, amounts, coverage, actualSourceSpanBuffer);
        AssertRgba32PEqual(expected, actual, colorMode, alphaMode, "SourceSpanAmountSpanCoverage");

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = BlendWithCoverageScalar(blender, associatedBackground[i], constantAssociatedSource, amounts[i], coverage[i]);
        }

        blender.BlendWithCoverage(Configuration.Default, actual, associatedBackground, constantAssociatedSource, amounts, coverage, actualConstantSourceBuffer);
        AssertRgba32PEqual(expected, actual, colorMode, alphaMode, "ConstantSourceAmountSpanCoverage");
    }

    private static Rgba32P BlendWithCoverageScalar(PixelBlender<Rgba32P> blender, Rgba32P background, Rgba32P source, float amount, float coverage)
    {
        Span<Rgba32P> destination = stackalloc Rgba32P[1];
        Span<Rgba32P> backgroundSpan = stackalloc Rgba32P[1] { background };
        Span<Rgba32P> sourceSpan = stackalloc Rgba32P[1] { source };
        Span<float> coverageSpan = stackalloc float[1] { coverage };
        Span<Vector4> buffer = stackalloc Vector4[3];

        // A one-pixel span takes the scalar remainder path, providing an exact oracle for each SIMD coverage result.
        blender.BlendWithCoverage<Rgba32P>(Configuration.Default, destination, backgroundSpan, sourceSpan, amount, coverageSpan, buffer);
        return destination[0];
    }

    private static void AssertRgba32PEqual(
        ReadOnlySpan<Rgba32P> expected,
        ReadOnlySpan<Rgba32P> actual,
        PixelColorBlendingMode colorMode,
        PixelAlphaCompositionMode alphaMode,
        string scenario)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(expected[i] == actual[i], $"{colorMode}/{alphaMode}/{scenario}[{i}]: expected {expected[i]}, actual {actual[i]}");
        }
    }

    private static void ExerciseBlender(PixelBlender<Rgba32> blender)
    {
        Rgba32 background = Color.MistyRose.ToPixel<Rgba32>();
        Rgba32 source = Color.MidnightBlue.ToPixel<Rgba32>();

        ExerciseBlender(blender, background, source);
    }

    /// <summary>
    /// Exercises every bulk overload across vector-width boundaries and compares it with single-pixel composition.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="blender">The blender under test.</param>
    /// <param name="background">The background pixel.</param>
    /// <param name="source">The source pixel.</param>
    private static void ExerciseBlender<TPixel>(PixelBlender<TPixel> blender, TPixel background, TPixel source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        const float amount = .625F;

        float[] amounts = [0F, .125F, .375F, .625F, .875F, 1F, .5F];
        float[] coverage = [1F, .8F, .6F, .4F, .2F, 0F, .5F];

        // Seven pixels exercise one AVX-512 batch plus three tails, or three AVX2 batches plus one tail.
        TPixel[] destination = new TPixel[7];
        TPixel[] expected = new TPixel[7];
        TPixel[] backgroundSpan = [background, background, background, background, background, background, background];
        TPixel[] sourceSpan = [source, source, source, source, source, source, source];
        Vector4[] sourceSpanBuffer = new Vector4[destination.Length * 3];
        Vector4[] constantSourceBuffer = new Vector4[destination.Length * 2];

        Array.Fill(expected, blender.Blend(background, source, amount));

        blender.Blend<TPixel>(Configuration.Default, destination, backgroundSpan, sourceSpan, amount, sourceSpanBuffer);
        Assert.Equal(expected, destination);

        blender.Blend(Configuration.Default, destination, backgroundSpan, source, amount, constantSourceBuffer);
        Assert.Equal(expected, destination);

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = blender.Blend(background, source, amounts[i]);
        }

        blender.Blend(Configuration.Default, destination, backgroundSpan, sourceSpan, amounts, sourceSpanBuffer);
        Assert.Equal(expected, destination);

        blender.Blend(Configuration.Default, destination, backgroundSpan, source, amounts, constantSourceBuffer);
        Assert.Equal(expected, destination);

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = BlendWithCoverageScalar(blender, background, source, amount, coverage[i]);
        }

        blender.BlendWithCoverage<TPixel>(Configuration.Default, destination, backgroundSpan, sourceSpan, amount, coverage, sourceSpanBuffer);
        Assert.Equal(expected, destination);

        blender.BlendWithCoverage(Configuration.Default, destination, backgroundSpan, source, amount, coverage, constantSourceBuffer);
        Assert.Equal(expected, destination);

        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = BlendWithCoverageScalar(blender, background, source, amounts[i], coverage[i]);
        }

        blender.BlendWithCoverage(Configuration.Default, destination, backgroundSpan, sourceSpan, amounts, coverage, sourceSpanBuffer);
        Assert.Equal(expected, destination);

        blender.BlendWithCoverage(Configuration.Default, destination, backgroundSpan, source, amounts, coverage, constantSourceBuffer);
        Assert.Equal(expected, destination);
    }

    /// <summary>
    /// Computes a one-pixel coverage result through the scalar remainder path.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="blender">The blender under test.</param>
    /// <param name="background">The background pixel.</param>
    /// <param name="source">The source pixel.</param>
    /// <param name="amount">The source opacity.</param>
    /// <param name="coverage">The pixel coverage.</param>
    /// <returns>The blended pixel.</returns>
    private static TPixel BlendWithCoverageScalar<TPixel>(
        PixelBlender<TPixel> blender,
        TPixel background,
        TPixel source,
        float amount,
        float coverage)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Span<TPixel> destination = stackalloc TPixel[1];
        Span<TPixel> backgroundSpan = stackalloc TPixel[1] { background };
        Span<TPixel> sourceSpan = stackalloc TPixel[1] { source };
        Span<float> coverageSpan = stackalloc float[1] { coverage };
        Span<Vector4> buffer = stackalloc Vector4[3];

        blender.BlendWithCoverage<TPixel>(
            Configuration.Default,
            destination,
            backgroundSpan,
            sourceSpan,
            amount,
            coverageSpan,
            buffer);

        return destination[0];
    }
}
