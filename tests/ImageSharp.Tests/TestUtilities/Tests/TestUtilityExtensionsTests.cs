// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public class TestUtilityExtensionsTests
    {
        public TestUtilityExtensionsTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        public static Image<TPixel> CreateTestImage<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var image = new Image<TPixel>(10, 10);

            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    var v = new Vector4(i, j, 0, 1);
                    v /= 10;

                    var color = default(TPixel);
                    color.PackFromVector4(v);

                    pixels[i, j] = color;
                }
            }

            return image;
        }

        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, true)]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, false)]
        public void IsEquivalentTo_WhenFalse<TPixel>(TestImageProvider<TPixel> provider, bool compareAlpha)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> a = provider.GetImage();
            Image<TPixel> b = provider.GetImage(x => x.OilPaint(3, 2));

            Assert.False(a.IsEquivalentTo(b, compareAlpha));
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Rgba32 | PixelTypes.Bgr565, true)]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Rgba32 | PixelTypes.Bgr565, false)]
        public void IsEquivalentTo_WhenTrue<TPixel>(TestImageProvider<TPixel> provider, bool compareAlpha)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> a = provider.GetImage();
            Image<TPixel> b = provider.GetImage();

            Assert.True(a.IsEquivalentTo(b, compareAlpha));
        }

        [Theory]
        [InlineData(PixelTypes.Rgba32, typeof(Rgba32))]
        [InlineData(PixelTypes.Argb32, typeof(Argb32))]
        [InlineData(PixelTypes.HalfVector4, typeof(HalfVector4))]
        public void ToType(PixelTypes pt, Type expectedType)
        {
            Assert.Equal(pt.GetClrType(), expectedType);
        }

        [Theory]
        [InlineData(typeof(Rgba32), PixelTypes.Rgba32)]
        [InlineData(typeof(Argb32), PixelTypes.Argb32)]
        public void GetPixelType(Type clrType, PixelTypes expectedPixelType)
        {
            Assert.Equal(expectedPixelType, clrType.GetPixelType());
        }

        private static void AssertContainsPixelType<T>(
            PixelTypes pt,
            IEnumerable<KeyValuePair<PixelTypes, Type>> pixelTypesExp)
        {
            Assert.Contains(new KeyValuePair<PixelTypes, Type>(pt, typeof(T)), pixelTypesExp);

        }

        [Fact]
        public void ExpandAllTypes_1()
        {
            PixelTypes pixelTypes = PixelTypes.Alpha8 | PixelTypes.Bgr565 | PixelTypes.HalfVector2 | PixelTypes.Rgba32;

            IEnumerable<KeyValuePair<PixelTypes, Type>> expanded = pixelTypes.ExpandAllTypes();

            Assert.Equal(4, expanded.Count());

            AssertContainsPixelType<Alpha8>(PixelTypes.Alpha8, expanded);
            AssertContainsPixelType<Bgr565>(PixelTypes.Bgr565, expanded);
            AssertContainsPixelType<HalfVector2>(PixelTypes.HalfVector2, expanded);
            AssertContainsPixelType<Rgba32>(PixelTypes.Rgba32, expanded);
        }

        [Fact]
        public void ExpandAllTypes_2()
        {
            PixelTypes pixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

            IEnumerable<KeyValuePair<PixelTypes, Type>> expanded = pixelTypes.ExpandAllTypes();

            Assert.Equal(3, expanded.Count());

            AssertContainsPixelType<Rgba32>(PixelTypes.Rgba32, expanded);
            AssertContainsPixelType<Bgra32>(PixelTypes.Bgra32, expanded);
            AssertContainsPixelType<RgbaVector>(PixelTypes.RgbaVector, expanded);
        }

        [Fact]
        public void ToTypes_All()
        {
            KeyValuePair<PixelTypes, Type>[] expanded = PixelTypes.All.ExpandAllTypes().ToArray();

            Assert.True(expanded.Length >= TestUtils.GetAllPixelTypes().Length - 2);
            AssertContainsPixelType<Rgba32>(PixelTypes.Rgba32, expanded);
            AssertContainsPixelType<Rgba32>(PixelTypes.Rgba32, expanded);
        }
    }
}
