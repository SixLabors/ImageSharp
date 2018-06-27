// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders
{
    using SixLabors.Memory;

    public class PorterDuffFunctionsTests_TPixel
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
            TPixel actual = PorterDuffFunctions.Normal_SrcOver((TPixel)(TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(NormalBlendFunctionData))]
        public void NormalBlendFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.Normal_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(NormalBlendFunctionData))]
        public void NormalBlendFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.Normal_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
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
            TPixel actual = PorterDuffFunctions.Multiply_SrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(MultiplyFunctionData))]
        public void MultiplyFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.Multiply_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(MultiplyFunctionData))]
        public void MultiplyFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.Multiply_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
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
            TPixel actual = PorterDuffFunctions.Add_SrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(AddFunctionData))]
        public void AddFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.Add_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(AddFunctionData))]
        public void AddFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.Add_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
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
            TPixel actual = PorterDuffFunctions.Subtract_SrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(SubstractFunctionData))]
        public void SubstractFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.Subtract_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(SubstractFunctionData))]
        public void SubstractFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.Subtract_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
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
            TPixel actual = PorterDuffFunctions.Screen_SrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(ScreenFunctionData))]
        public void ScreenFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.Screen_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(ScreenFunctionData))]
        public void ScreenFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.Screen_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
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
            TPixel actual = PorterDuffFunctions.Darken_SrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(DarkenFunctionData))]
        public void DarkenFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.Darken_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(DarkenFunctionData))]
        public void DarkenFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.Darken_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
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
            TPixel actual = PorterDuffFunctions.Lighten_SrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(LightenFunctionData))]
        public void LightenFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.Lighten_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(LightenFunctionData))]
        public void LightenFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.Lighten_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
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
            TPixel actual = PorterDuffFunctions.Overlay_SrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(OverlayFunctionData))]
        public void OverlayFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.Overlay_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(OverlayFunctionData))]
        public void OverlayFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.Overlay_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
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
            TPixel actual = PorterDuffFunctions.HardLight_SrcOver((TPixel)back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(HardLightFunctionData))]
        public void HardLightFunction_Blender<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel actual = new DefaultPixelBlenders<TPixel>.HardLight_SrcOver().Blend(back, source, amount);
            VectorAssert.Equal(expected, actual, 2);
        }

        [Theory]
        [MemberData(nameof(HardLightFunctionData))]
        public void HardLightFunction_Blender_Bulk<TPixel>(TestPixel<TPixel> back, TestPixel<TPixel> source, float amount, TestPixel<TPixel> expected)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> dest = new Span<TPixel>(new TPixel[1]);
            new DefaultPixelBlenders<TPixel>.HardLight_SrcOver().Blend(this.MemoryAllocator, dest, back.AsSpan(), source.AsSpan(), AsSpan(amount));
            VectorAssert.Equal(expected, dest[0], 2);
        }
    }
}
