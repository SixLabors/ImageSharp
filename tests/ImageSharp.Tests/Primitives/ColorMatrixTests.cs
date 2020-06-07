// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Primitives
{
    public class ColorMatrixTests
    {
        private readonly ApproximateFloatComparer approximateFloatComparer = new ApproximateFloatComparer(1e-6f);

        [Fact]
        public void ColorMatrixIdentityIsCorrect()
        {
            ColorMatrix val = default;
            val.M11 = val.M22 = val.M33 = val.M44 = 1F;

            Assert.Equal(val, ColorMatrix.Identity, this.approximateFloatComparer);
        }

        [Fact]
        public void ColorMatrixCanDetectIdentity()
        {
            ColorMatrix m = ColorMatrix.Identity;
            Assert.True(m.IsIdentity);

            m.M12 = 1F;
            Assert.False(m.IsIdentity);
        }

        [Fact]
        public void ColorMatrixEquality()
        {
            ColorMatrix m = KnownFilterMatrices.CreateHueFilter(45F);
            ColorMatrix m2 = KnownFilterMatrices.CreateHueFilter(45F);
            object obj = m2;

            Assert.True(m.Equals(obj));
            Assert.True(m.Equals(m2));
            Assert.True(m == m2);
            Assert.False(m != m2);
        }

        [Fact]
        public void ColorMatrixMultiply()
        {
            ColorMatrix value1 = this.CreateAllTwos();
            ColorMatrix value2 = this.CreateAllThrees();

            var m = default(ColorMatrix);

            // First row
            m.M11 = (value1.M11 * value2.M11) + (value1.M12 * value2.M21) + (value1.M13 * value2.M31) + (value1.M14 * value2.M41);
            m.M12 = (value1.M11 * value2.M12) + (value1.M12 * value2.M22) + (value1.M13 * value2.M32) + (value1.M14 * value2.M42);
            m.M13 = (value1.M11 * value2.M13) + (value1.M12 * value2.M23) + (value1.M13 * value2.M33) + (value1.M14 * value2.M43);
            m.M14 = (value1.M11 * value2.M14) + (value1.M12 * value2.M24) + (value1.M13 * value2.M34) + (value1.M14 * value2.M44);

            // Second row
            m.M21 = (value1.M21 * value2.M11) + (value1.M22 * value2.M21) + (value1.M23 * value2.M31) + (value1.M24 * value2.M41);
            m.M22 = (value1.M21 * value2.M12) + (value1.M22 * value2.M22) + (value1.M23 * value2.M32) + (value1.M24 * value2.M42);
            m.M23 = (value1.M21 * value2.M13) + (value1.M22 * value2.M23) + (value1.M23 * value2.M33) + (value1.M24 * value2.M43);
            m.M24 = (value1.M21 * value2.M14) + (value1.M22 * value2.M24) + (value1.M23 * value2.M34) + (value1.M24 * value2.M44);

            // Third row
            m.M31 = (value1.M31 * value2.M11) + (value1.M32 * value2.M21) + (value1.M33 * value2.M31) + (value1.M34 * value2.M41);
            m.M32 = (value1.M31 * value2.M12) + (value1.M32 * value2.M22) + (value1.M33 * value2.M32) + (value1.M34 * value2.M42);
            m.M33 = (value1.M31 * value2.M13) + (value1.M32 * value2.M23) + (value1.M33 * value2.M33) + (value1.M34 * value2.M43);
            m.M34 = (value1.M31 * value2.M14) + (value1.M32 * value2.M24) + (value1.M33 * value2.M34) + (value1.M34 * value2.M44);

            // Fourth row
            m.M41 = (value1.M41 * value2.M11) + (value1.M42 * value2.M21) + (value1.M43 * value2.M31) + (value1.M44 * value2.M41);
            m.M42 = (value1.M41 * value2.M12) + (value1.M42 * value2.M22) + (value1.M43 * value2.M32) + (value1.M44 * value2.M42);
            m.M43 = (value1.M41 * value2.M13) + (value1.M42 * value2.M23) + (value1.M43 * value2.M33) + (value1.M44 * value2.M43);
            m.M44 = (value1.M41 * value2.M14) + (value1.M42 * value2.M24) + (value1.M43 * value2.M34) + (value1.M44 * value2.M44);

            // Fifth row
            m.M51 = (value1.M51 * value2.M11) + (value1.M52 * value2.M21) + (value1.M53 * value2.M31) + (value1.M54 * value2.M41) + value2.M51;
            m.M52 = (value1.M51 * value2.M12) + (value1.M52 * value2.M22) + (value1.M53 * value2.M32) + (value1.M54 * value2.M52) + value2.M52;
            m.M53 = (value1.M51 * value2.M13) + (value1.M52 * value2.M23) + (value1.M53 * value2.M33) + (value1.M54 * value2.M53) + value2.M53;
            m.M54 = (value1.M51 * value2.M14) + (value1.M52 * value2.M24) + (value1.M53 * value2.M34) + (value1.M54 * value2.M54) + value2.M54;

            Assert.Equal(m, value1 * value2, this.approximateFloatComparer);
        }

        [Fact]
        public void ColorMatrixMultiplyScalar()
        {
            ColorMatrix m = this.CreateAllTwos();
            Assert.Equal(this.CreateAllFours(), m * 2, this.approximateFloatComparer);
        }

        [Fact]
        public void ColorMatrixSubtract()
        {
            ColorMatrix m = this.CreateAllOnes() + this.CreateAllTwos();
            Assert.Equal(this.CreateAllThrees(), m);
        }

        [Fact]
        public void ColorMatrixNegate()
        {
            ColorMatrix m = this.CreateAllOnes() * -1F;
            Assert.Equal(m, -this.CreateAllOnes());
        }

        [Fact]
        public void ColorMatrixAdd()
        {
            ColorMatrix m = this.CreateAllOnes() + this.CreateAllTwos();
            Assert.Equal(this.CreateAllThrees(), m);
        }

        [Fact]
        public void ColorMatrixHashCode()
        {
#if NETCOREAPP2_1
            ColorMatrix m = KnownFilterMatrices.CreateBrightnessFilter(.5F);
            HashCode hash = default;
            hash.Add(m.M11);
            hash.Add(m.M12);
            hash.Add(m.M13);
            hash.Add(m.M14);
            hash.Add(m.M21);
            hash.Add(m.M22);
            hash.Add(m.M23);
            hash.Add(m.M24);
            hash.Add(m.M31);
            hash.Add(m.M32);
            hash.Add(m.M33);
            hash.Add(m.M34);
            hash.Add(m.M41);
            hash.Add(m.M42);
            hash.Add(m.M43);
            hash.Add(m.M44);
            hash.Add(m.M51);
            hash.Add(m.M52);
            hash.Add(m.M53);
            hash.Add(m.M54);

            Assert.Equal(hash.ToHashCode(), m.GetHashCode());
#endif
        }

        [Fact]
        public void ColorMatrixToString()
        {
            ColorMatrix m = KnownFilterMatrices.CreateBrightnessFilter(.5F);

            CultureInfo ci = CultureInfo.CurrentCulture;

#pragma warning disable SA1117 // Parameters should be on same line or separate lines
            string expected = string.Format(ci, "{{ {{M11:{0} M12:{1} M13:{2} M14:{3}}} {{M21:{4} M22:{5} M23:{6} M24:{7}}} {{M31:{8} M32:{9} M33:{10} M34:{11}}} {{M41:{12} M42:{13} M43:{14} M44:{15}}} {{M51:{16} M52:{17} M53:{18} M54:{19}}} }}",
                m.M11.ToString(ci), m.M12.ToString(ci), m.M13.ToString(ci), m.M14.ToString(ci),
                m.M21.ToString(ci), m.M22.ToString(ci), m.M23.ToString(ci), m.M24.ToString(ci),
                m.M31.ToString(ci), m.M32.ToString(ci), m.M33.ToString(ci), m.M34.ToString(ci),
                m.M41.ToString(ci), m.M42.ToString(ci), m.M43.ToString(ci), m.M44.ToString(ci),
                m.M51.ToString(ci), m.M52.ToString(ci), m.M53.ToString(ci), m.M54.ToString(ci));
#pragma warning restore SA1117 // Parameters should be on same line or separate lines

            Assert.Equal(expected, m.ToString());
        }

        private ColorMatrix CreateAllOnes()
        {
            return new ColorMatrix
            {
                M11 = 1F,
                M12 = 1F,
                M13 = 1F,
                M14 = 1F,
                M21 = 1F,
                M22 = 1F,
                M23 = 1F,
                M24 = 1F,
                M31 = 1F,
                M32 = 1F,
                M33 = 1F,
                M34 = 1F,
                M41 = 1F,
                M42 = 1F,
                M43 = 1F,
                M44 = 1F,
                M51 = 1F,
                M52 = 1F,
                M53 = 1F,
                M54 = 1F
            };
        }

        private ColorMatrix CreateAllTwos()
        {
            return new ColorMatrix
            {
                M11 = 2F,
                M12 = 2F,
                M13 = 2F,
                M14 = 2F,
                M21 = 2F,
                M22 = 2F,
                M23 = 2F,
                M24 = 2F,
                M31 = 2F,
                M32 = 2F,
                M33 = 2F,
                M34 = 2F,
                M41 = 2F,
                M42 = 2F,
                M43 = 2F,
                M44 = 2F,
                M51 = 2F,
                M52 = 2F,
                M53 = 2F,
                M54 = 2F
            };
        }

        private ColorMatrix CreateAllThrees()
        {
            return new ColorMatrix
            {
                M11 = 3F,
                M12 = 3F,
                M13 = 3F,
                M14 = 3F,
                M21 = 3F,
                M22 = 3F,
                M23 = 3F,
                M24 = 3F,
                M31 = 3F,
                M32 = 3F,
                M33 = 3F,
                M34 = 3F,
                M41 = 3F,
                M42 = 3F,
                M43 = 3F,
                M44 = 3F,
                M51 = 3F,
                M52 = 3F,
                M53 = 3F,
                M54 = 3F
            };
        }

        private ColorMatrix CreateAllFours()
        {
            return new ColorMatrix
            {
                M11 = 4F,
                M12 = 4F,
                M13 = 4F,
                M14 = 4F,
                M21 = 4F,
                M22 = 4F,
                M23 = 4F,
                M24 = 4F,
                M31 = 4F,
                M32 = 4F,
                M33 = 4F,
                M34 = 4F,
                M41 = 4F,
                M42 = 4F,
                M43 = 4F,
                M44 = 4F,
                M51 = 4F,
                M52 = 4F,
                M53 = 4F,
                M54 = 4F
            };
        }
    }
}
