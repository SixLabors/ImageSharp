
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
    using Moq;
    using System.Collections.Immutable;

    public class ShapePathTests
    {
        private readonly Mock<IPath> pathMock1;
        private readonly Mock<IPath> pathMock2;
        private readonly SixLabors.Shapes.Rectangle bounds1;

        public ShapePathTests()
        {
            this.pathMock2 = new Mock<IPath>();
            this.pathMock1 = new Mock<IPath>();

            this.bounds1 = new SixLabors.Shapes.Rectangle(10.5f, 10, 10, 10);
            pathMock1.Setup(x => x.Bounds).Returns(this.bounds1);
            pathMock2.Setup(x => x.Bounds).Returns(this.bounds1);
            // wire up the 2 mocks to reference eachother
            pathMock1.Setup(x => x.AsClosedPath()).Returns(() => pathMock2.Object);
        }

        [Fact]
        public void ShapePathFromPathConvertsBoundsDoesNotProxyToShape()
        {
            ShapePath region = new ShapePath(pathMock1.Object);

            Assert.Equal(Math.Floor(bounds1.Left), region.Bounds.Left);
            Assert.Equal(Math.Ceiling(bounds1.Right), region.Bounds.Right);

            pathMock1.Verify(x => x.Bounds);
        }

        [Fact]
        public void ShapePathFromPathMaxIntersectionsProxyToShape()
        {
            ShapePath region = new ShapePath(pathMock1.Object);

            int i = region.MaxIntersections;
            pathMock1.Verify(x => x.MaxIntersections);
        }

        [Fact]
        public void ShapePathFromPathScanXProxyToShape()
        {
            int xToScan = 10;
            ShapePath region = new ShapePath(pathMock1.Object);

            pathMock1.Setup(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<Vector2, Vector2, Vector2[], int, int>((s, e, b, c, o) => {
                    Assert.Equal(xToScan, s.X);
                    Assert.Equal(xToScan, e.X);
                    Assert.True(s.Y < bounds1.Top);
                    Assert.True(e.Y > bounds1.Bottom);
                }).Returns(0);

            int i = region.ScanX(xToScan, new float[0], 0, 0);

            pathMock1.Verify(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void ShapePathFromPathScanYProxyToShape()
        {
            int yToScan = 10;
            ShapePath region = new ShapePath(pathMock1.Object);

            pathMock1.Setup(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<Vector2, Vector2, Vector2[], int, int>((s, e, b, c, o) => {
                    Assert.Equal(yToScan, s.Y);
                    Assert.Equal(yToScan, e.Y);
                    Assert.True(s.X < bounds1.Left);
                    Assert.True(e.X > bounds1.Right);
                }).Returns(0);

            int i = region.ScanY(yToScan, new float[0], 0, 0);

            pathMock1.Verify(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }


        [Fact]
        public void ShapePathFromShapeScanXProxyToShape()
        {
            int xToScan = 10;
            ShapePath region = new ShapePath(pathMock1.Object);

            pathMock1.Setup(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<Vector2, Vector2, Vector2[], int, int>((s, e, b, c, o) => {
                    Assert.Equal(xToScan, s.X);
                    Assert.Equal(xToScan, e.X);
                    Assert.True(s.Y < bounds1.Top);
                    Assert.True(e.Y > bounds1.Bottom);
                }).Returns(0);

            int i = region.ScanX(xToScan, new float[0], 0, 0);

            pathMock1.Verify(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }
        
        [Fact]
        public void GetPointInfoCallSinglePathForPath()
        {
            ShapePath region = new ShapePath(pathMock1.Object);

            ImageSharp.Drawing.PointInfo info = region.GetPointInfo(10, 1);

            pathMock1.Verify(x => x.Distance(new Vector2(10, 1)), Times.Once);
            pathMock2.Verify(x => x.Distance(new Vector2(10, 1)), Times.Never);
        }
    }
}
