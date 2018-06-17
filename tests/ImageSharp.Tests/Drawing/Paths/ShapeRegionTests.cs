// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;
using System;
using System.Collections.Generic;
using System.Numerics;
using Moq;
using SixLabors.Primitives;
using SixLabors.Shapes;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Paths
{
    public class ShapeRegionTests
    {
        public abstract class MockPath : IPath
        {
            public abstract RectangleF Bounds { get; }
            public IPath AsClosedPath() => this;

            public abstract SegmentInfo PointAlongPath(float distanceAlongPath);
            public abstract PointInfo Distance(PointF point);
            public abstract IEnumerable<ISimplePath> Flatten();
            public abstract bool Contains(PointF point);
            public abstract IPath Transform(Matrix3x2 matrix);
            public abstract PathTypes PathType { get; }
            public abstract int MaxIntersections { get; }
            public abstract float Length { get; }

            public int FindIntersections(PointF start, PointF end, PointF[] buffer, int offset)
            {
                return this.FindIntersections(start, end, buffer, 0);
            }

            public int FindIntersections(PointF s, PointF e, Span<PointF> buffer)
            {
                Assert.Equal(this.TestYToScan, s.Y);
                Assert.Equal(this.TestYToScan, e.Y);
                Assert.True(s.X < this.Bounds.Left);
                Assert.True(e.X > this.Bounds.Right);

                this.TestFindIntersectionsInvocationCounter++;

                return this.TestFindIntersectionsResult;
            }

            public int TestFindIntersectionsInvocationCounter { get; private set; }
            public virtual int TestYToScan => 10;
            public virtual int TestFindIntersectionsResult => 3;
        }

        private readonly Mock<MockPath> pathMock;

        private readonly RectangleF bounds;

        public ShapeRegionTests()
        {
            this.pathMock = new Mock<MockPath>() { CallBase = true };

            this.bounds = new RectangleF(10.5f, 10, 10, 10);
            this.pathMock.Setup(x => x.Bounds).Returns(this.bounds);
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
            MockPath path = this.pathMock.Object;
            int yToScan = path.TestYToScan;
            var region = new ShapeRegion(path);

            int i = region.Scan(yToScan, new float[path.TestFindIntersectionsResult], Configuration.Default);

            Assert.Equal(path.TestFindIntersectionsResult, i);
            Assert.Equal(1, path.TestFindIntersectionsInvocationCounter);
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