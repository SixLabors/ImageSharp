
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

    public class ShapeRegionTests 
    {
        private readonly Mock<IPath> pathMock;
        private readonly Mock<IShape> shapeMock;
        private readonly SixLabors.Shapes.Rectangle bounds;

        public ShapeRegionTests()
        {
            this.shapeMock = new Mock<IShape>();
            this.pathMock = new Mock<IPath>();

            this.bounds = new SixLabors.Shapes.Rectangle(10.5f, 10, 10, 10);
            shapeMock.Setup(x => x.Bounds).Returns(this.bounds);
            // wire up the 2 mocks to reference eachother
            pathMock.Setup(x => x.AsShape()).Returns(() => shapeMock.Object);
            shapeMock.Setup(x => x.Paths).Returns(() => ImmutableArray.Create(pathMock.Object));
        }

        [Fact]
        public void ShapeRegionWithPathCallsAsShape()
        {
            new ShapeRegion(pathMock.Object);

            pathMock.Verify(x => x.AsShape());
        }

        [Fact]
        public void ShapeRegionWithPathRetainsShape()
        {
            ShapeRegion region = new ShapeRegion(pathMock.Object);

            Assert.Equal(shapeMock.Object, region.Shape);
        }

        [Fact]
        public void ShapeRegionFromPathConvertsBoundsProxyToShape()
        {
            ShapeRegion region = new ShapeRegion(pathMock.Object);

            Assert.Equal(Math.Floor(bounds.Left), region.Bounds.Left);
            Assert.Equal(Math.Ceiling(bounds.Right), region.Bounds.Right);

            shapeMock.Verify(x => x.Bounds);
        }

        [Fact]
        public void ShapeRegionFromPathMaxIntersectionsProxyToShape()
        {
            ShapeRegion region = new ShapeRegion(pathMock.Object);

            int i = region.MaxIntersections;
            shapeMock.Verify(x => x.MaxIntersections);
        }

        [Fact]
        public void ShapeRegionFromPathScanXProxyToShape()
        {
            int xToScan = 10;
            ShapeRegion region = new ShapeRegion(pathMock.Object);

            shapeMock.Setup(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<Vector2, Vector2, Vector2[], int, int>((s, e, b, c, o) => {
                    Assert.Equal(xToScan, s.X);
                    Assert.Equal(xToScan, e.X);
                    Assert.True(s.Y < bounds.Top);
                    Assert.True(e.Y > bounds.Bottom);
                }).Returns(0);

            int i = region.ScanX(xToScan, new float[0], 0, 0);

            shapeMock.Verify(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void ShapeRegionFromPathScanYProxyToShape()
        {
            int yToScan = 10;
            ShapeRegion region = new ShapeRegion(pathMock.Object);

            shapeMock.Setup(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<Vector2, Vector2, Vector2[], int, int>((s, e, b, c, o) => {
                    Assert.Equal(yToScan, s.Y);
                    Assert.Equal(yToScan, e.Y);
                    Assert.True(s.X < bounds.Left);
                    Assert.True(e.X > bounds.Right);
                }).Returns(0);

            int i = region.ScanY(yToScan, new float[0], 0, 0);

            shapeMock.Verify(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }


        [Fact]
        public void ShapeRegionFromShapeScanXProxyToShape()
        {
            int xToScan = 10;
            ShapeRegion region = new ShapeRegion(shapeMock.Object);

            shapeMock.Setup(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<Vector2, Vector2, Vector2[], int, int>((s, e, b, c, o) => {
                    Assert.Equal(xToScan, s.X);
                    Assert.Equal(xToScan, e.X);
                    Assert.True(s.Y < bounds.Top);
                    Assert.True(e.Y > bounds.Bottom);
                }).Returns(0);

            int i = region.ScanX(xToScan, new float[0], 0, 0);

            shapeMock.Verify(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void ShapeRegionFromShapeScanYProxyToShape()
        {
            int yToScan = 10;
            ShapeRegion region = new ShapeRegion(shapeMock.Object);

            shapeMock.Setup(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<Vector2, Vector2, Vector2[], int, int>((s, e, b, c, o) => {
                    Assert.Equal(yToScan, s.Y);
                    Assert.Equal(yToScan, e.Y);
                    Assert.True(s.X < bounds.Left);
                    Assert.True(e.X > bounds.Right);
                }).Returns(0);

            int i = region.ScanY(yToScan, new float[0], 0, 0);

            shapeMock.Verify(x => x.FindIntersections(It.IsAny<Vector2>(), It.IsAny<Vector2>(), It.IsAny<Vector2[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void ShapeRegionFromShapeConvertsBoundsProxyToShape()
        {
            ShapeRegion region = new ShapeRegion(shapeMock.Object);

            Assert.Equal(Math.Floor(bounds.Left), region.Bounds.Left);
            Assert.Equal(Math.Ceiling(bounds.Right), region.Bounds.Right);

            shapeMock.Verify(x => x.Bounds);
        }

        [Fact]
        public void ShapeRegionFromShapeMaxIntersectionsProxyToShape()
        {
            ShapeRegion region = new ShapeRegion(shapeMock.Object);

            int i = region.MaxIntersections;
            shapeMock.Verify(x => x.MaxIntersections);
        }
    }
}
