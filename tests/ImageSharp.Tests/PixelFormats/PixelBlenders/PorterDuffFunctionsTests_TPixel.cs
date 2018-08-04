// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders
{
    using SixLabors.Memory;

    public class PorterDuffFunctionsTestsTPixel
    {
        private static Span<T> AsSpan<T>(T value)
            where T : struct
        {
            return new Span<T>(new[] { value });
        }

        public static TheoryData<object, object, float, object> NormalBlendFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(1,1,1,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(0.6f, 0.6f, 0.6f, 1) },
        };

        private MemoryAllocator MemoryAllocator { get; } = Configuration.Default.MemoryAllocator;

        [Theory]
        [MemberData(nameof(NormalBlendFunctionData))]
        public void NormalBlendFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.NormalSrcOver((TPixel)(TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(NormalBlendFunctionData))]
        public void NormalBlendFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.NormalSrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(NormalBlendFunctionData))]
        public void NormalBlendFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.NormalSrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }

        public static TheoryData<object, object, float, object> MultiplyFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(1,1,1,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(0.6f, 0.6f, 0.6f, 1) },
            {
                new TestPixel<Rgba32>(0.9f,0.9f,0.9f,0.9f),
                new TestPixel<Rgba32>(0.4f,0.4f,0.4f,0.4f),
                .5f,
                new TestPixel<Rgba32>(0.7834783f, 0.7834783f, 0.7834783f, 0.92f)
            },
        };

        [Theory]
        [MemberData(nameof(MultiplyFunctionData))]
        public void MultiplyFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.MultiplySrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(MultiplyFunctionData))]
        public void MultiplyFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.MultiplySrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(MultiplyFunctionData))]
        public void MultiplyFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.MultiplySrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }

        public static TheoryData<object, object, float, object> AddFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(1,1,1,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(1f, 1f, 1f, 1f) },
            {
                new TestPixel<Rgba32>(0.2f,0.2f,0.2f,0.3f),
                new TestPixel<Rgba32>(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestPixel<Rgba32>(.2431373f, .2431373f, .2431373f, .372549f)
            },
        };

        [Theory]
        [MemberData(nameof(AddFunctionData))]
        public void AddFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.AddSrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(AddFunctionData))]
        public void AddFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.AddSrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(AddFunctionData))]
        public void AddFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.AddSrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }

        public static TheoryData<object, object, float, object> SubstractFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(0,0,0,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(1,1,1, 1f) },
            {
                new TestPixel<Rgba32>(0.2f,0.2f,0.2f,0.3f),
                new TestPixel<Rgba32>(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestPixel<Rgba32>(.2027027f, .2027027f, .2027027f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(SubstractFunctionData))]
        public void SubstractFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.SubtractSrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(SubstractFunctionData))]
        public void SubstractFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.SubtractSrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(SubstractFunctionData))]
        public void SubstractFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.SubtractSrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }

        public static TheoryData<object, object, float, object> ScreenFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(1,1,1,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(1,1,1, 1f) },
            {
                new TestPixel<Rgba32>(0.2f,0.2f,0.2f,0.3f),
                new TestPixel<Rgba32>(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestPixel<Rgba32>(.2383784f, .2383784f, .2383784f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(ScreenFunctionData))]
        public void ScreenFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.ScreenSrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(ScreenFunctionData))]
        public void ScreenFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.ScreenSrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(ScreenFunctionData))]
        public void ScreenFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.ScreenSrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }

        public static TheoryData<object, object, float, object> DarkenFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(1,1,1,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(.6f,.6f,.6f, 1f) },
            {
                new TestPixel<Rgba32>(0.2f,0.2f,0.2f,0.3f),
                new TestPixel<Rgba32>(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestPixel<Rgba32>(.2189189f, .2189189f, .2189189f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(DarkenFunctionData))]
        public void DarkenFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.DarkenSrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(DarkenFunctionData))]
        public void DarkenFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.DarkenSrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(DarkenFunctionData))]
        public void DarkenFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.DarkenSrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }

        public static TheoryData<object, object, float, object> LightenFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(1,1,1,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(1,1,1,1f) },
            {
                new TestPixel<Rgba32>(0.2f,0.2f,0.2f,0.3f),
                new TestPixel<Rgba32>(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestPixel<Rgba32>(.227027f, .227027f, .227027f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(LightenFunctionData))]
        public void LightenFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.LightenSrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(LightenFunctionData))]
        public void LightenFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.LightenSrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(LightenFunctionData))]
        public void LightenFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.LightenSrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }

        public static TheoryData<object, object, float, object> OverlayFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(1,1,1,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(1,1,1,1f) },
            {
                new TestPixel<Rgba32>(0.2f,0.2f,0.2f,0.3f),
                new TestPixel<Rgba32>(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestPixel<Rgba32>(.2124324f, .2124324f, .2124324f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(OverlayFunctionData))]
        public void OverlayFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.OverlaySrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(OverlayFunctionData))]
        public void OverlayFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.OverlaySrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(OverlayFunctionData))]
        public void OverlayFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.OverlaySrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }

        public static TheoryData<object, object, float, object> HardLightFunctionData = new TheoryData<object, object, float, object>() {
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(1,1,1,1), 1, new TestPixel<Rgba32>(1,1,1,1) },
            { new TestPixel<Rgba32>(1,1,1,1), new TestPixel<Rgba32>(0,0,0,.8f), .5f, new TestPixel<Rgba32>(0.6f,0.6f,0.6f,1f) },
            {
                new TestPixel<Rgba32>(0.2f,0.2f,0.2f,0.3f),
                new TestPixel<Rgba32>(0.3f,0.3f,0.3f,0.2f),
                .5f,
                new TestPixel<Rgba32>(.2124324f, .2124324f, .2124324f, .37f)
            },
        };

        [Theory]
        [MemberData(nameof(HardLightFunctionData))]
        public void HardLightFunction<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = PorterDuffFunctions.HardLightSrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(HardLightFunctionData))]
        public void HardLightFunctionBlender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.HardLightSrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(HardLightFunctionData))]
        public void HardLightFunctionBlenderBulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            var dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.HardLightSrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }
    }
}
