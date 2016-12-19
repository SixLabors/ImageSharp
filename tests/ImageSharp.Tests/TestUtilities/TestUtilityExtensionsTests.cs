// <copyright file="FlagsHelper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;

    using Xunit;
    using Xunit.Abstractions;

    public class TestUtilityExtensionsTests
    {
        public TestUtilityExtensionsTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        public static Image<TColor, TPacked> CreateTestImage<TColor, TPacked>()
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            Image<TColor, TPacked> image = new Image<TColor, TPacked>(10, 10);

            using (var pixels = image.Lock())
            {
                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        Vector4 v = new Vector4(i, j, 0, 1);
                        v /= 10;

                        TColor color = default(TColor);
                        color.PackFromVector4(v);

                        pixels[i, j] = color;
                    }
                }
            }

            return image;
        }

        [Fact]
        public void Baz()
        {
            var type = typeof(Color).GetTypeInfo().Assembly.GetType("ImageSharp.Color");
            this.Output.WriteLine(type.ToString());

            var fake = typeof(Color).GetTypeInfo().Assembly.GetType("ImageSharp.dsaada_DASqewrr");
            Assert.Null(fake);
        }

        [Fact]
        public void GetPackedType()
        {
            Type shouldBeUIint32 = TestUtilityExtensions.GetPackedType(typeof(Color));

            Assert.Equal(shouldBeUIint32, typeof(uint));
        }

        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Color)]
        public void IsEquivalentTo_WhenFalse<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var a = factory.Create();
            var b = factory.Create();
            b = b.OilPaint(3, 2);

            Assert.False(a.IsEquivalentTo(b));
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Color | PixelTypes.Bgr565)]
        public void IsEquivalentTo_WhenTrue<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var a = factory.Create();
            var b = factory.Create();

            Assert.True(a.IsEquivalentTo(b));
        }

        [Theory]
        [InlineData(PixelTypes.Color, typeof(Color))]
        [InlineData(PixelTypes.Argb, typeof(Argb))]
        [InlineData(PixelTypes.HalfVector4, typeof(HalfVector4))]
        public void ToType(PixelTypes pt, Type expectedType)
        {
            Assert.Equal(pt.ToType(), expectedType);
        }

        [Fact]
        public void ToTypes()
        {
            PixelTypes pixelTypes = PixelTypes.Alpha8 | PixelTypes.Bgr565 | PixelTypes.Color | PixelTypes.HalfVector2;

            var clrTypes = pixelTypes.ToTypes().ToArray();

            Assert.Equal(clrTypes.Length, 4);
            Assert.Contains(typeof(Alpha8), clrTypes);
            Assert.Contains(typeof(Bgr565), clrTypes);
            Assert.Contains(typeof(Color), clrTypes);
            Assert.Contains(typeof(HalfVector2), clrTypes);
        }

        [Fact]
        public void ToTypes_All()
        {
            var clrTypes = PixelTypes.All.ToTypes().ToArray();

            Assert.True(clrTypes.Length >= FlagsHelper<PixelTypes>.GetSortedValues().Length - 2);
        }
    }
}