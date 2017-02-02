
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;
    using System.IO;
    using ImageSharp;
    using ImageSharp.Drawing.Brushes;
    using Processing;
    using System.Collections.Generic;
    using Xunit;
    using ImageSharp.Drawing;
    using System.Numerics;
    using SixLabors.Shapes;
    using ImageSharp.Drawing.Processors;
    using ImageSharp.Drawing.Pens;

    public class Extensions 
    {
        [Fact]
        public void ConvertPointInfo()
        {
            SixLabors.Shapes.PointInfo src =  new SixLabors.Shapes.PointInfo
            {
                ClosestPointOnPath = Vector2.UnitX,
                SearchPoint = Vector2.UnitY,
                DistanceAlongPath = 99f,
                DistanceFromPath = 82f
            };
            ImageSharp.Drawing.PointInfo info = src.Convert();

            Assert.Equal(src.DistanceAlongPath, info.DistanceAlongPath);
            Assert.Equal(src.DistanceFromPath, info.DistanceFromPath);
        }

        [Theory]
        [InlineData(0.5, 0.5, 5, 5, 0,0,6,6)]
        [InlineData(1, 1, 5, 5, 1,1,5,5)]
        public void ConvertRectangle(float x, float y, float width, float height, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
        {
            SixLabors.Shapes.Rectangle src = new SixLabors.Shapes.Rectangle(x, y, width, height);
            ImageSharp.Rectangle actual = src.Convert();

            Assert.Equal(expectedX, actual.X);
            Assert.Equal(expectedY, actual.Y);
            Assert.Equal(expectedWidth, actual.Width);
            Assert.Equal(expectedHeight, actual.Height);
        }
    }
}
