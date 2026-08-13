// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders;

public class PorterDuffFunctionsTests
{
    private static readonly ApproximateFloatComparer FloatComparer = new(.000001F);

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> NormalBlendFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(0.6f, 0.6f, 0.6f, 1) }
    };

    [Theory]
    [MemberData(nameof(NormalBlendFunctionData))]
    public void NormalBlendFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.NormalSrcOver((Vector4)back, source, amount);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(NormalBlendFunctionData))]
    public void NormalBlendFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.NormalSrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(NormalBlendFunctionData))]
    public void NormalBlendFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.NormalSrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> MultiplyFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(0.6f, 0.6f, 0.6f, 1) },
        { new TestVector4(0.9f, 0.9f, 0.9f, 0.9f), new TestVector4(0.4f, 0.4f, 0.4f, 0.4f), .5f, new TestVector4(0.7834783f, 0.7834783f, 0.7834783f, 0.92f) }
    };

    [Theory]
    [MemberData(nameof(MultiplyFunctionData))]
    public void MultiplyFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.MultiplySrcOver((Vector4)back, source, amount);
        VectorAssert.Equal(expected, actual, 5);
    }

    [Theory]
    [MemberData(nameof(MultiplyFunctionData))]
    public void MultiplyFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.MultiplySrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(MultiplyFunctionData))]
    public void MultiplyFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.MultiplySrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> AddFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(0.2f, 0.2f, 0.2f, 0.3f), new TestVector4(0.3f, 0.3f, 0.3f, 0.2f), .5f, new TestVector4(0.24324325f, 0.24324325f, 0.24324325f, .37f) }
    };

    [Theory]
    [MemberData(nameof(AddFunctionData))]
    public void AddFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.AddSrcOver((Vector4)back, source, amount);
        VectorAssert.Equal(expected, actual, 5);
    }

    [Theory]
    [MemberData(nameof(AddFunctionData))]
    public void AddFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.AddSrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(AddFunctionData))]
    public void AddFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.AddSrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> SubtractFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(0, 0, 0, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(1, 1, 1, 1f) },
        { new TestVector4(0.2f, 0.2f, 0.2f, 0.3f), new TestVector4(0.3f, 0.3f, 0.3f, 0.2f), .5f, new TestVector4(.2027027f, .2027027f, .2027027f, .37f) }
    };

    [Theory]
    [MemberData(nameof(SubtractFunctionData))]
    public void SubtractFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.SubtractSrcOver((Vector4)back, source, amount);
        VectorAssert.Equal(expected, actual, 5);
    }

    [Theory]
    [MemberData(nameof(SubtractFunctionData))]
    public void SubtractFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.SubtractSrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(SubtractFunctionData))]
    public void SubtractFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.SubtractSrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> ScreenFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(1, 1, 1, 1f) },
        { new TestVector4(0.2f, 0.2f, 0.2f, 0.3f), new TestVector4(0.3f, 0.3f, 0.3f, 0.2f), .5f, new TestVector4(.2383784f, .2383784f, .2383784f, .37f) }
    };

    [Theory]
    [MemberData(nameof(ScreenFunctionData))]
    public void ScreenFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.ScreenSrcOver((Vector4)back, source, amount);
        VectorAssert.Equal(expected, actual, 5);
    }

    [Theory]
    [MemberData(nameof(ScreenFunctionData))]
    public void ScreenFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.ScreenSrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(ScreenFunctionData))]
    public void ScreenFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.ScreenSrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> DarkenFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(.6f, .6f, .6f, 1f) },
        { new TestVector4(0.2f, 0.2f, 0.2f, 0.3f), new TestVector4(0.3f, 0.3f, 0.3f, 0.2f), .5f, new TestVector4(.2189189f, .2189189f, .2189189f, .37f) }
    };

    [Theory]
    [MemberData(nameof(DarkenFunctionData))]
    public void DarkenFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.DarkenSrcOver((Vector4)back, source, amount);
        VectorAssert.Equal(expected, actual, 5);
    }

    [Theory]
    [MemberData(nameof(DarkenFunctionData))]
    public void DarkenFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.DarkenSrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(DarkenFunctionData))]
    public void DarkenFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.DarkenSrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> LightenFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(1, 1, 1, 1f) },
        { new TestVector4(0.2f, 0.2f, 0.2f, 0.3f), new TestVector4(0.3f, 0.3f, 0.3f, 0.2f), .5f, new TestVector4(.227027f, .227027f, .227027f, .37f) },
    };

    [Theory]
    [MemberData(nameof(LightenFunctionData))]
    public void LightenFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.LightenSrcOver((Vector4)back, source, amount);
        VectorAssert.Equal(expected, actual, 5);
    }

    [Theory]
    [MemberData(nameof(LightenFunctionData))]
    public void LightenFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.LightenSrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(LightenFunctionData))]
    public void LightenFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.LightenSrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> OverlayFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(1, 1, 1, 1f) },
        { new TestVector4(0.2f, 0.2f, 0.2f, 0.3f), new TestVector4(0.3f, 0.3f, 0.3f, 0.2f), .5f, new TestVector4(.2124324f, .2124324f, .2124324f, .37f) },
    };

    [Theory]
    [MemberData(nameof(OverlayFunctionData))]
    public void OverlayFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.OverlaySrcOver((Vector4)back, source, amount);
        VectorAssert.Equal(expected, actual, 5);
    }

    [Theory]
    [MemberData(nameof(OverlayFunctionData))]
    public void OverlayFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.OverlaySrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(OverlayFunctionData))]
    public void OverlayFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.OverlaySrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> HardLightFunctionData { get; } = new()
    {
        { new TestVector4(1, 1, 1, 1), new TestVector4(1, 1, 1, 1), 1, new TestVector4(1, 1, 1, 1) },
        { new TestVector4(1, 1, 1, 1), new TestVector4(0, 0, 0, .8f), .5f, new TestVector4(0.6f, 0.6f, 0.6f, 1f) },
        { new TestVector4(0.2f, 0.2f, 0.2f, 0.3f), new TestVector4(0.3f, 0.3f, 0.3f, 0.2f), .5f, new TestVector4(.2124324f, .2124324f, .2124324f, .37f) },
    };

    [Theory]
    [MemberData(nameof(HardLightFunctionData))]
    public void HardLightFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        Vector4 actual = PorterDuffFunctions.HardLightSrcOver((Vector4)back, source, amount);
        VectorAssert.Equal(expected, actual, 5);
    }

    [Theory]
    [MemberData(nameof(HardLightFunctionData))]
    public void HardLightFunction256(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> back256 = Vector256.Create(back.X, back.Y, back.Z, back.W, back.X, back.Y, back.Z, back.W);
        Vector256<float> source256 = Vector256.Create(source.X, source.Y, source.Z, source.W, source.X, source.Y, source.Z, source.W);

        Vector256<float> expected256 = Vector256.Create(expected.X, expected.Y, expected.Z, expected.W, expected.X, expected.Y, expected.Z, expected.W);
        Vector256<float> actual = PorterDuffFunctions.HardLightSrcOver(back256, source256, Vector256.Create(amount));
        Assert.Equal(expected256, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(HardLightFunctionData))]
    public void HardLightFunction512(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> back512 = CreateVector512(back);
        Vector512<float> source512 = CreateVector512(source);
        Vector512<float> expected512 = CreateVector512(expected);
        Vector512<float> actual = PorterDuffFunctions.HardLightSrcOver(back512, source512, Vector512.Create(amount));
        Assert.Equal(expected512, actual, FloatComparer);
    }

    public static TheoryData<PixelColorBlendingMode, TestVector4, TestVector4, TestVector4> ExtendedBlendFunctionData { get; } = new()
    {
        { PixelColorBlendingMode.ColorDodge, CreateTestVector(0F, .25F, .8F, 1F), CreateTestVector(1F, .5F, .5F, 1F), CreateTestVector(0F, .5F, 1F, 1F) },
        { PixelColorBlendingMode.ColorBurn, CreateTestVector(1F, .4F, .75F, 1F), CreateTestVector(0F, 0F, .5F, 1F), CreateTestVector(1F, 0F, .5F, 1F) },
        { PixelColorBlendingMode.SoftLight, CreateTestVector(.25F, .64F, .16F, 1F), CreateTestVector(.75F, .75F, .25F, 1F), CreateTestVector(.375F, .72F, .0928F, 1F) },
        { PixelColorBlendingMode.SoftLight, CreateTestVector(.25F, .36F, .81F, 1F), CreateTestVector(.5F, .5F, .5F, 1F), CreateTestVector(.25F, .36F, .81F, 1F) },
        { PixelColorBlendingMode.Difference, CreateTestVector(.1F, .8F, .4F, 1F), CreateTestVector(.7F, .2F, .4F, 1F), CreateTestVector(.6F, .6F, 0F, 1F) },
        { PixelColorBlendingMode.Exclusion, CreateTestVector(.1F, .8F, .4F, 1F), CreateTestVector(.7F, .2F, .4F, 1F), CreateTestVector(.66F, .68F, .48F, 1F) },
        { PixelColorBlendingMode.Hue, CreateTestVector(.2F, .7F, .4F, 1F), CreateTestVector(.9F, .1F, .6F, 1F), CreateTestVector(.832625F, .332625F, .645125F, 1F) },
        { PixelColorBlendingMode.Saturation, CreateTestVector(.2F, .7F, .4F, 1F), CreateTestVector(.9F, .1F, .6F, 1F), CreateTestVector(.0098F, .8098F, .3298F, 1F) },
        { PixelColorBlendingMode.Color, CreateTestVector(.2F, .7F, .4F, 1F), CreateTestVector(.9F, .1F, .6F, 1F), CreateTestVector(1F, .234851485F, .713069307F, 1F) },
        { PixelColorBlendingMode.Luminosity, CreateTestVector(.2F, .7F, .4F, 1F), CreateTestVector(.9F, .1F, .6F, 1F), CreateTestVector(.078F, .578F, .278F, 1F) },
        { PixelColorBlendingMode.Hue, CreateTestVector(.5F, .5F, .5F, 1F), CreateTestVector(.9F, .2F, .6F, 1F), CreateTestVector(.5F, .5F, .5F, 1F) },
        { PixelColorBlendingMode.Saturation, CreateTestVector(.5F, .5F, .5F, 1F), CreateTestVector(.9F, .2F, .6F, 1F), CreateTestVector(.5F, .5F, .5F, 1F) },
    };

    [Theory]
    [MemberData(nameof(ExtendedBlendFunctionData))]
    public void ExtendedBlendFunction(PixelColorBlendingMode mode, TestVector4 back, TestVector4 source, TestVector4 expected)
    {
        Vector4 actual = InvokeExtendedBlend(mode, back, source, 1F);
        VectorAssert.Equal(expected, actual, 4);
    }

    [Theory]
    [MemberData(nameof(ExtendedBlendFunctionData))]
    public void ExtendedBlendFunction256(PixelColorBlendingMode mode, TestVector4 back, TestVector4 source, TestVector4 expected)
    {
        if (!Avx.IsSupported)
        {
            return;
        }

        Vector256<float> actual = InvokeExtendedBlend(mode, Vector256.Create(back.AsVector().AsVector128(), back.AsVector().AsVector128()), Vector256.Create(source.AsVector().AsVector128(), source.AsVector().AsVector128()), Vector256.Create(1F));
        Vector256<float> expectedVector = Vector256.Create(expected.AsVector().AsVector128(), expected.AsVector().AsVector128());
        Assert.Equal(expectedVector, actual, FloatComparer);
    }

    [Theory]
    [MemberData(nameof(ExtendedBlendFunctionData))]
    public void ExtendedBlendFunction512(PixelColorBlendingMode mode, TestVector4 back, TestVector4 source, TestVector4 expected)
    {
        if (!Avx512F.IsSupported)
        {
            return;
        }

        Vector512<float> actual = InvokeExtendedBlend(mode, CreateVector512(back), CreateVector512(source), Vector512.Create(1F));
        Assert.Equal(CreateVector512(expected), actual, FloatComparer);
    }

    /// <summary>
    /// Creates serializable vector test data without relying on <see cref="TestVector4.TestVector4(float, float, float, float)"/>,
    /// which assigns <c>Z</c> from <c>X</c> and can hide channel-specific regressions.
    /// </summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    /// <param name="w">The W component.</param>
    /// <returns>The test vector.</returns>
    private static TestVector4 CreateTestVector(float x, float y, float z, float w)
        => new() { X = x, Y = y, Z = z, W = w };

    /// <summary>
    /// Invokes a new blend mode through its straight-alpha source-over function.
    /// </summary>
    /// <param name="mode">The color blending mode.</param>
    /// <param name="backdrop">The backdrop vector.</param>
    /// <param name="source">The source vector.</param>
    /// <param name="amount">The source amount.</param>
    /// <returns>The composition result.</returns>
    private static Vector4 InvokeExtendedBlend(PixelColorBlendingMode mode, Vector4 backdrop, Vector4 source, float amount)
        => mode switch
        {
            PixelColorBlendingMode.ColorDodge => PorterDuffFunctions.ColorDodgeSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.ColorBurn => PorterDuffFunctions.ColorBurnSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.SoftLight => PorterDuffFunctions.SoftLightSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Difference => PorterDuffFunctions.DifferenceSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Exclusion => PorterDuffFunctions.ExclusionSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Hue => PorterDuffFunctions.HueSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Saturation => PorterDuffFunctions.SaturationSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Color => PorterDuffFunctions.ColorSrcOver(backdrop, source, amount),
            _ => PorterDuffFunctions.LuminositySrcOver(backdrop, source, amount),
        };

    /// <inheritdoc cref="InvokeExtendedBlend(PixelColorBlendingMode, Vector4, Vector4, float)" />
    private static Vector256<float> InvokeExtendedBlend(PixelColorBlendingMode mode, Vector256<float> backdrop, Vector256<float> source, Vector256<float> amount)
        => mode switch
        {
            PixelColorBlendingMode.ColorDodge => PorterDuffFunctions.ColorDodgeSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.ColorBurn => PorterDuffFunctions.ColorBurnSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.SoftLight => PorterDuffFunctions.SoftLightSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Difference => PorterDuffFunctions.DifferenceSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Exclusion => PorterDuffFunctions.ExclusionSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Hue => PorterDuffFunctions.HueSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Saturation => PorterDuffFunctions.SaturationSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Color => PorterDuffFunctions.ColorSrcOver(backdrop, source, amount),
            _ => PorterDuffFunctions.LuminositySrcOver(backdrop, source, amount),
        };

    /// <inheritdoc cref="InvokeExtendedBlend(PixelColorBlendingMode, Vector4, Vector4, float)" />
    private static Vector512<float> InvokeExtendedBlend(PixelColorBlendingMode mode, Vector512<float> backdrop, Vector512<float> source, Vector512<float> amount)
        => mode switch
        {
            PixelColorBlendingMode.ColorDodge => PorterDuffFunctions.ColorDodgeSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.ColorBurn => PorterDuffFunctions.ColorBurnSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.SoftLight => PorterDuffFunctions.SoftLightSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Difference => PorterDuffFunctions.DifferenceSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Exclusion => PorterDuffFunctions.ExclusionSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Hue => PorterDuffFunctions.HueSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Saturation => PorterDuffFunctions.SaturationSrcOver(backdrop, source, amount),
            PixelColorBlendingMode.Color => PorterDuffFunctions.ColorSrcOver(backdrop, source, amount),
            _ => PorterDuffFunctions.LuminositySrcOver(backdrop, source, amount),
        };

    private static Vector512<float> CreateVector512(TestVector4 vector)
        => Vector512.Create(
            vector.X,
            vector.Y,
            vector.Z,
            vector.W,
            vector.X,
            vector.Y,
            vector.Z,
            vector.W,
            vector.X,
            vector.Y,
            vector.Z,
            vector.W,
            vector.X,
            vector.Y,
            vector.Z,
            vector.W);
}
