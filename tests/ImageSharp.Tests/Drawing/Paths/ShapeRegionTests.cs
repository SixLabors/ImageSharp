// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Tests.Drawing.Paths
{
    using System;

    using Moq;
    using SixLabors.Primitives;
    using SixLabors.Shapes;

    using Xunit;

    public class ShapeRegionTests
    {
        private readonly Mock<IPath> pathMock;

        private readonly RectangleF bounds;

        public ShapeRegionTests()
        {
            this.pathMock = new Mock<IPath>();

            this.bounds = new RectangleF(10.5f, 10, 10, 10);
            this.pathMock.Setup(x => x.Bounds).Returns(this.bounds);
            // wire up the 2 mocks to reference eachother
            this.pathMock.Setup(x => x.AsClosedPath()).Returns(() => this.pathMock.Object);
        }

        [Fact]
        public void ShapeRegionWithPathCallsAsShape()
        {
            new ShapeRegion(this.pathMock.Object);

            this.pathMock.Verify(x => x.AsClosedPath());
        }

        [Fact]
        public void ShapeRegionWithPathRetainsShape()
        {
            var region = new ShapeRegion(this.pathMock.Object);

            Assert.Equal(this.pathMock.Object, region.Shape);
        }

        [Fact]
        public void ShapeRegionFromPathConvertsBoundsProxyToShape()
        {
            var region = new ShapeRegion(this.pathMock.Object);

            Assert.Equal(Math.Floor(this.bounds.Left), region.Bounds.Left);
            Assert.Equal(Math.Ceiling(this.bounds.Right), region.Bounds.Right);

            this.pathMock.Verify(x => x.Bounds);
        }

        [Fact]
        public void ShapeRegionFromPathMaxIntersectionsProxyToShape()
        {
            var region = new ShapeRegion(this.pathMock.Object);

            int i = region.MaxIntersections;
            this.pathMock.Verify(x => x.MaxIntersections);
        }

        [Fact]
        public void ShapeRegionFromPathScanYProxyToShape()
        {
            int yToScan = 10;
            var region = new ShapeRegion(this.pathMock.Object);

            this.pathMock
                .Setup(
                    x => x.FindIntersections(
                        It.IsAny<PointF>(),
                        It.IsAny<PointF>(),
                        It.IsAny<PointF[]>(),
                        It.IsAny<int>())).Callback<PointF, PointF, PointF[], int>(
                    (s, e, b, o) =>
                        {
                            Assert.Equal(yToScan, s.Y);
                            Assert.Equal(yToScan, e.Y);
                            Assert.True(s.X < this.bounds.Left);
                            Assert.True(e.X > this.bounds.Right);
                        }).Returns(0);

            int i = region.Scan(yToScan, new float[0], Configuration.Default);

            this.pathMock.Verify(
                x => x.FindIntersections(It.IsAny<PointF>(), It.IsAny<PointF>(), It.IsAny<PointF[]>(), It.IsAny<int>()),
                Times.Once);
        }

        [Fact]
        public void ShapeRegionFromShapeScanYProxyToShape()
        {
            int yToScan = 10;
            var region = new ShapeRegion(this.pathMock.Object);

            this.pathMock
                .Setup(
                    x => x.FindIntersections(
                        It.IsAny<PointF>(),
                        It.IsAny<PointF>(),
                        It.IsAny<PointF[]>(),
                        It.IsAny<int>())).Callback<PointF, PointF, PointF[], int>(
                    (s, e, b, o) =>
                        {
                            Assert.Equal(yToScan, s.Y);
                            Assert.Equal(yToScan, e.Y);
                            Assert.True(s.X < this.bounds.Left);
                            Assert.True(e.X > this.bounds.Right);
                        }).Returns(0);

            int i = region.Scan(yToScan, new float[0], Configuration.Default);

            this.pathMock.Verify(
                x => x.FindIntersections(It.IsAny<PointF>(), It.IsAny<PointF>(), It.IsAny<PointF[]>(), It.IsAny<int>()),
                Times.Once);
        }

        [Fact]
        public void ShapeRegionFromShapeConvertsBoundsProxyToShape()
        {
            var region = new ShapeRegion(this.pathMock.Object);

            Assert.Equal(Math.Floor(this.bounds.Left), region.Bounds.Left);
            Assert.Equal(Math.Ceiling(this.bounds.Right), region.Bounds.Right);

            this.pathMock.Verify(x => x.Bounds);
        }

        [Fact]
        public void ShapeRegionFromShapeMaxIntersectionsProxyToShape()
        {
            var region = new ShapeRegion(this.pathMock.Object);

            int i = region.MaxIntersections;
            this.pathMock.Verify(x => x.MaxIntersections);
        }
    }
}