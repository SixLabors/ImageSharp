// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Test implementations of IEquatable and IAlmostEquatable in our colorspaces
    /// </summary>
    public class ColorSpaceEqualityTests
    {
        [Fact]
        public void CieLabEquality()
        {
            var x = default(CieLab);
            var y = new CieLab(Vector3.One);

            Assert.True(default(CieLab) == default(CieLab));
            Assert.True(default(CieLab) != new CieLab(1, 0, 1));
            Assert.False(default(CieLab) == new CieLab(1, 0, 1));
            Assert.Equal(default(CieLab), default(CieLab));
            Assert.Equal(new CieLab(1, 0, 1), new CieLab(1, 0, 1));
            Assert.Equal(new CieLab(Vector3.One), new CieLab(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void CieLchEquality()
        {
            var x = default(CieLch);
            var y = new CieLch(Vector3.One);
            Assert.Equal(default(CieLch), default(CieLch));
            Assert.Equal(new CieLch(1, 0, 1), new CieLch(1, 0, 1));
            Assert.Equal(new CieLch(Vector3.One), new CieLch(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void CieLchuvEquality()
        {
            var x = default(CieLchuv);
            var y = new CieLchuv(Vector3.One);
            Assert.Equal(default(CieLchuv), default(CieLchuv));
            Assert.Equal(new CieLchuv(1, 0, 1), new CieLchuv(1, 0, 1));
            Assert.Equal(new CieLchuv(Vector3.One), new CieLchuv(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void CieLuvEquality()
        {
            var x = default(CieLuv);
            var y = new CieLuv(Vector3.One);
            Assert.Equal(default(CieLuv), default(CieLuv));
            Assert.Equal(new CieLuv(1, 0, 1), new CieLuv(1, 0, 1));
            Assert.Equal(new CieLuv(Vector3.One), new CieLuv(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void CieXyzEquality()
        {
            var x = default(CieXyz);
            var y = new CieXyz(Vector3.One);
            Assert.Equal(default(CieXyz), default(CieXyz));
            Assert.Equal(new CieXyz(1, 0, 1), new CieXyz(1, 0, 1));
            Assert.Equal(new CieXyz(Vector3.One), new CieXyz(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void CieXyyEquality()
        {
            var x = default(CieXyy);
            var y = new CieXyy(Vector3.One);
            Assert.Equal(default(CieXyy), default(CieXyy));
            Assert.Equal(new CieXyy(1, 0, 1), new CieXyy(1, 0, 1));
            Assert.Equal(new CieXyy(Vector3.One), new CieXyy(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void HslEquality()
        {
            var x = default(Hsl);
            var y = new Hsl(Vector3.One);
            Assert.Equal(default(Hsl), default(Hsl));
            Assert.Equal(new Hsl(1, 0, 1), new Hsl(1, 0, 1));
            Assert.Equal(new Hsl(Vector3.One), new Hsl(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void HsvEquality()
        {
            var x = default(Hsv);
            var y = new Hsv(Vector3.One);
            Assert.Equal(default(Hsv), default(Hsv));
            Assert.Equal(new Hsv(1, 0, 1), new Hsv(1, 0, 1));
            Assert.Equal(new Hsv(Vector3.One), new Hsv(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void HunterLabEquality()
        {
            var x = default(HunterLab);
            var y = new HunterLab(Vector3.One);
            Assert.Equal(default(HunterLab), default(HunterLab));
            Assert.Equal(new HunterLab(1, 0, 1), new HunterLab(1, 0, 1));
            Assert.Equal(new HunterLab(Vector3.One), new HunterLab(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void LmsEquality()
        {
            var x = default(Lms);
            var y = new Lms(Vector3.One);
            Assert.Equal(default(Lms), default(Lms));
            Assert.Equal(new Lms(1, 0, 1), new Lms(1, 0, 1));
            Assert.Equal(new Lms(Vector3.One), new Lms(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void LinearRgbEquality()
        {
            var x = default(LinearRgb);
            var y = new LinearRgb(Vector3.One);
            Assert.Equal(default(LinearRgb), default(LinearRgb));
            Assert.Equal(new LinearRgb(1, 0, 1), new LinearRgb(1, 0, 1));
            Assert.Equal(new LinearRgb(Vector3.One), new LinearRgb(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void RgbEquality()
        {
            var x = default(Rgb);
            var y = new Rgb(Vector3.One);
            Assert.Equal(default(Rgb), default(Rgb));
            Assert.Equal(new Rgb(1, 0, 1), new Rgb(1, 0, 1));
            Assert.Equal(new Rgb(Vector3.One), new Rgb(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void YCbCrEquality()
        {
            var x = default(YCbCr);
            var y = new YCbCr(Vector3.One);
            Assert.Equal(default(YCbCr), default(YCbCr));
            Assert.Equal(new YCbCr(1, 0, 1), new YCbCr(1, 0, 1));
            Assert.Equal(new YCbCr(Vector3.One), new YCbCr(Vector3.One));
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void CmykEquality()
        {
            var x = default(Cmyk);
            var y = new Cmyk(Vector4.One);
            Assert.Equal(default(Cmyk), default(Cmyk));
            Assert.Equal(new Cmyk(1, 0, 1, 0), new Cmyk(1, 0, 1, 0));
            Assert.Equal(new Cmyk(Vector4.One), new Cmyk(Vector4.One));
            Assert.False(x.Equals(y));
        }
    }
}
