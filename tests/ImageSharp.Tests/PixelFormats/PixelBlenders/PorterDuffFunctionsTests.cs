// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Castle.Components.DictionaryAdapter;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders;

public class PorterDuffFunctionsTests
{
    private static readonly ApproximateFloatComparer FloatComparer = new(.000001F);

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> NormalBlendFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(1, 1, 1, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(0.6f, 0.6f, 0.6f, 1) }
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

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> MultiplyFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(1, 1, 1, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(0.6f, 0.6f, 0.6f, 1) },
        { new(0.9f, 0.9f, 0.9f, 0.9f), new(0.4f, 0.4f, 0.4f, 0.4f), .5f, new(0.7834783f, 0.7834783f, 0.7834783f, 0.92f) }
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

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> AddFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(1, 1, 1, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(1, 1, 1, 1) },
        { new(0.2f, 0.2f, 0.2f, 0.3f), new(0.3f, 0.3f, 0.3f, 0.2f), .5f, new(0.24324325f, 0.24324325f, 0.24324325f, .37f) }
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

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> SubtractFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(0, 0, 0, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(1, 1, 1, 1f) },
        { new(0.2f, 0.2f, 0.2f, 0.3f), new(0.3f, 0.3f, 0.3f, 0.2f), .5f, new(.2027027f, .2027027f, .2027027f, .37f) }
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

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> ScreenFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(1, 1, 1, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(1, 1, 1, 1f) },
        { new(0.2f, 0.2f, 0.2f, 0.3f), new(0.3f, 0.3f, 0.3f, 0.2f), .5f, new(.2383784f, .2383784f, .2383784f, .37f) }
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

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> DarkenFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(1, 1, 1, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(.6f, .6f, .6f, 1f) },
        { new(0.2f, 0.2f, 0.2f, 0.3f), new(0.3f, 0.3f, 0.3f, 0.2f), .5f, new(.2189189f, .2189189f, .2189189f, .37f) }
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

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> LightenFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(1, 1, 1, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(1, 1, 1, 1f) },
        { new(0.2f, 0.2f, 0.2f, 0.3f), new(0.3f, 0.3f, 0.3f, 0.2f), .5f, new(.227027f, .227027f, .227027f, .37f) },
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

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> OverlayFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(1, 1, 1, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(1, 1, 1, 1f) },
        { new(0.2f, 0.2f, 0.2f, 0.3f), new(0.3f, 0.3f, 0.3f, 0.2f), .5f, new(.2124324f, .2124324f, .2124324f, .37f) },
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

    public static TheoryData<TestVector4, TestVector4, float, TestVector4> HardLightFunctionData { get; } = new()
    {
        { new(1, 1, 1, 1), new(1, 1, 1, 1), 1, new(1, 1, 1, 1) },
        { new(1, 1, 1, 1), new(0, 0, 0, .8f), .5f, new(0.6f, 0.6f, 0.6f, 1f) },
        { new(0.2f, 0.2f, 0.2f, 0.3f), new(0.3f, 0.3f, 0.3f, 0.2f), .5f, new(.2124324f, .2124324f, .2124324f, .37f) },
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
}
