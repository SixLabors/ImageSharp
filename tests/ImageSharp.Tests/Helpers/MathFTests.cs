namespace ImageSharp.Tests.Helpers
{
    using System;

    using Xunit;

    public class MathFTests
    {
        [Fact]
        public void MathF_PI_Is_Equal()
        {
            Assert.Equal(MathF.PI, (float)Math.PI);
        }

        [Fact]
        public void MathF_Ceililng_Is_Equal()
        {
            Assert.Equal(MathF.Ceiling(0.3333F), (float)Math.Ceiling(0.3333F));
        }

        [Fact]
        public void MathF_Cos_Is_Equal()
        {
            Assert.Equal(MathF.Cos(0.3333F), (float)Math.Cos(0.3333F));
        }

        [Fact]
        public void MathF_Abs_Is_Equal()
        {
            Assert.Equal(MathF.Abs(-0.3333F), (float)Math.Abs(-0.3333F));
        }

        [Fact]
        public void MathF_Atan2_Is_Equal()
        {
            Assert.Equal(MathF.Atan2(1.2345F, 1.2345F), (float)Math.Atan2(1.2345F, 1.2345F));
        }

        [Fact]
        public void MathF_Exp_Is_Equal()
        {
            Assert.Equal(MathF.Exp(1.2345F), (float)Math.Exp(1.2345F));
        }

        [Fact]
        public void MathF_Floor_Is_Equal()
        {
            Assert.Equal(MathF.Floor(1.2345F), (float)Math.Floor(1.2345F));
        }

        [Fact]
        public void MathF_Min_Is_Equal()
        {
            Assert.Equal(MathF.Min(1.2345F, 5.4321F), (float)Math.Min(1.2345F, 5.4321F));
        }

        [Fact]
        public void MathF_Max_Is_Equal()
        {
            Assert.Equal(MathF.Max(1.2345F, 5.4321F), (float)Math.Max(1.2345F, 5.4321F));
        }

        [Fact]
        public void MathF_Pow_Is_Equal()
        {
            Assert.Equal(MathF.Pow(1.2345F, 5.4321F), (float)Math.Pow(1.2345F, 5.4321F));
        }

        [Fact]
        public void MathF_Sin_Is_Equal()
        {
            Assert.Equal(MathF.Sin(1.2345F), (float)Math.Sin(1.2345F));
        }

        [Fact]
        public void MathF_Sqrt_Is_Equal()
        {
            Assert.Equal(MathF.Sqrt(2F), (float)Math.Sqrt(2F));
        }

        [Fact]
        public void Convert_Degree_To_Radian()
        {
            Assert.Equal((float)(Math.PI / 2D), MathF.DegreeToRadian(90F), new FloatRoundingComparer(6));
        }

        [Fact]
        public void Convert_Radian_To_Degree()
        {
            Assert.Equal(60F, MathF.RadianToDegree((float)(Math.PI / 3D)), new FloatRoundingComparer(5));
        }
    }
}