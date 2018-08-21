// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders
{
    public class PorterDuffFunctionsTests
    {
        public static TheoryData<TestVector4, TestVector4, float, TestVector4> NormalBlendFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(1,1,1,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(0.6f, 0.6f, 0.6f, 1) },
        };

        [Theory]
        [MemberData(nameof(NormalBlendFunctionData))]
        public void NormalBlendFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.NormalSrcOver((Vector4)back, source, amount);
            Assert.Equal(expected, actual);
        }

        public static TheoryData<TestVector4, TestVector4, float, TestVector4> MultiplyFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(1,1,1,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(0.6f, 0.6f, 0.6f, 1) },
            {
                new TestVector4(0.9f,0.9f,0.9f,0.9f),
                new TestVector4(0.4f,0.4f,0.4f,0.4f),
                .5f,
                new TestVector4(0.7834783f, 0.7834783f, 0.7834783f, 0.92f)
            },
        };

        [Theory]
        [MemberData(nameof(MultiplyFunctionData))]
        public void MultiplyFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.MultiplySrcOver((Vector4)back, source, amount);
            VectorAssert.Equal(expected, actual, 5);
        }

        public static TheoryData<TestVector4, TestVector4, float, TestVector4> AddFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(1,1,1,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(.6f, .6f, .6f, 1f) },
            {
                new TestVector4(0.2f,0.2f,0.2f,0.3f),
                new TestVector4(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestVector4(.2075676f, .2075676f, .2075676f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(AddFunctionData))]
        public void AddFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.MultiplySrcOver((Vector4)back, source, amount);
            VectorAssert.Equal(expected, actual, 5);
        }

        public static TheoryData<TestVector4, TestVector4, float, TestVector4> SubstractFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(0,0,0,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(1,1,1, 1f) },
            {
                new TestVector4(0.2f,0.2f,0.2f,0.3f),
                new TestVector4(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestVector4(.2027027f, .2027027f, .2027027f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(SubstractFunctionData))]
        public void SubstractFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.SubtractSrcOver((Vector4)back, source, amount);
            VectorAssert.Equal(expected, actual, 5);
        }

        public static TheoryData<TestVector4, TestVector4, float, TestVector4> ScreenFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(1,1,1,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(1,1,1, 1f) },
            {
                new TestVector4(0.2f,0.2f,0.2f,0.3f),
                new TestVector4(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestVector4(.2383784f, .2383784f, .2383784f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(ScreenFunctionData))]
        public void ScreenFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.ScreenSrcOver((Vector4)back, source, amount);
            VectorAssert.Equal(expected, actual, 5);
        }

        public static TheoryData<TestVector4, TestVector4, float, TestVector4> DarkenFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(1,1,1,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(.6f,.6f,.6f, 1f) },
            {
                new TestVector4(0.2f,0.2f,0.2f,0.3f),
                new TestVector4(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestVector4(.2189189f, .2189189f, .2189189f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(DarkenFunctionData))]
        public void DarkenFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.DarkenSrcOver((Vector4)back, source, amount);
            VectorAssert.Equal(expected, actual, 5);
        }

        public static TheoryData<TestVector4, TestVector4, float, TestVector4> LightenFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(1,1,1,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(1,1,1,1f) },
            {
                new TestVector4(0.2f,0.2f,0.2f,0.3f),
                new TestVector4(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestVector4(.227027f, .227027f, .227027f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(LightenFunctionData))]
        public void LightenFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.LightenSrcOver((Vector4)back, source, amount);
            VectorAssert.Equal(expected, actual, 5);
        }

        public static TheoryData<TestVector4, TestVector4, float, TestVector4> OverlayFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(1,1,1,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(1,1,1,1f) },
            {
                new TestVector4(0.2f,0.2f,0.2f,0.3f),
                new TestVector4(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestVector4(.2124324f, .2124324f, .2124324f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(OverlayFunctionData))]
        public void OverlayFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.OverlaySrcOver((Vector4)back, source, amount);
            VectorAssert.Equal(expected, actual, 5);
        }

        public static TheoryData<TestVector4, TestVector4, float, TestVector4> HardLightFunctionData = new TheoryData<TestVector4, TestVector4, float, TestVector4>() {
            { new TestVector4(1,1,1,1), new TestVector4(1,1,1,1), 1, new TestVector4(1,1,1,1) },
            { new TestVector4(1,1,1,1), new TestVector4(0,0,0,.8f), .5f, new TestVector4(0.6f,0.6f,0.6f,1f) },
            {
                new TestVector4(0.2f,0.2f,0.2f,0.3f),
                new TestVector4(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestVector4(.2124324f, .2124324f, .2124324f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(HardLightFunctionData))]
        public void HardLightFunction(TestVector4 back, TestVector4 source, float amount, TestVector4 expected)
        {
            Vector4 actual = PorterDuffFunctions.HardLightSrcOver((Vector4)back, source, amount);
            VectorAssert.Equal(expected, actual, 5);
        }
    }
}
