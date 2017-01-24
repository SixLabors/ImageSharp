// <copyright file="ComplexPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Shapes
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    using Paths;
    using PolygonClipper;

    /// <summary>
    /// Represents a complex polygon made up of one or more outline
    /// polygons and one or more holes to punch out of them.
    /// </summary>
    /// <seealso cref="ImageSharp.Drawing.Shapes.IShape" />
    public sealed class ComplexPolygon : IShape
    {
        private const float ClipperScaleFactor = 100f;
        private IShape[] shapes;
        private IEnumerable<IPath> paths;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplexPolygon"/> class.
        /// </summary>
        /// <param name="outline">The outline.</param>
        /// <param name="holes">The holes.</param>
        public ComplexPolygon(IShape outline, params IShape[] holes)
            : this(new[] { outline }, holes)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplexPolygon"/> class.
        /// </summary>
        /// <param name="outlines">The outlines.</param>
        /// <param name="holes">The holes.</param>
        public ComplexPolygon(IShape[] outlines, IShape[] holes)
        {
            Guard.NotNull(outlines, nameof(outlines));
            Guard.MustBeGreaterThanOrEqualTo(outlines.Length, 1, nameof(outlines));

            this.MaxIntersections = this.FixAndSetShapes(outlines, holes);

            float minX = this.shapes.Min(x => x.Bounds.Left);
            float maxX = this.shapes.Max(x => x.Bounds.Right);
            float minY = this.shapes.Min(x => x.Bounds.Top);
            float maxY = this.shapes.Max(x => x.Bounds.Bottom);

            this.Bounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Gets the bounding box of this shape.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        public RectangleF Bounds { get; }

        /// <summary>
        /// Gets the maximum number intersections that a shape can have when testing a line.
        /// </summary>
        /// <value>
        /// The maximum intersections.
        /// </value>
        public int MaxIntersections { get; }

        /// <summary>
        /// the distance of the point from the outline of the shape, if the value is negative it is inside the polygon bounds
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// Returns the distance from thr shape to the point
        /// </returns>
        /// <remarks>
        /// Due to the clipping we did during construction we know that out shapes do not overlap at there edges
        /// therefore for apoint to be in more that one we must be in a hole of another, theoretically this could
        /// then flip again to be in a outlin inside a hole inside an outline :)
        /// </remarks>
        float IShape.Distance(Vector2 point)
        {
            float dist = float.MaxValue;
            bool inside = false;
            foreach (IShape shape in this.shapes)
            {
                float d = shape.Distance(point);

                if (d <= 0)
                {
                    // we are inside a poly
                    d = -d;  // flip the sign
                    inside ^= true; // flip the inside flag
                }

                if (d < dist)
                {
                    dist = d;
                }
            }

            if (inside)
            {
                return -dist;
            }

            return dist;
        }

        /// <summary>
        /// Based on a line described by <paramref name="start"/> and <paramref name="end"/>
        /// populate a buffer for all points on all the polygons, that make up this complex shape,
        /// that the line intersects.
        /// </summary>
        /// <param name="start">The start point of the line.</param>
        /// <param name="end">The end point of the line.</param>
        /// <param name="buffer">The buffer that will be populated with intersections.</param>
        /// <param name="count">The count.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>
        /// The number of intersections populated into the buffer.
        /// </returns>
        public int FindIntersections(Vector2 start, Vector2 end, Vector2[] buffer, int count, int offset)
        {
            int totalAdded = 0;
            for (int i = 0; i < this.shapes.Length; i++)
            {
                int added = this.shapes[i].FindIntersections(start, end, buffer, count, offset);
                count -= added;
                offset += added;
                totalAdded += added;
            }

            return totalAdded;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<IPath> GetEnumerator()
        {
            return this.paths.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private void AddPoints(Clipper clipper, IShape shape, PolyType polyType)
        {
            // if the path is already the shape use it directly and skip the path loop.
            if (shape is IPath)
            {
                clipper.AddPath(
                      (IPath)shape,
                      polyType);
            }
            else
            {
                foreach (IPath path in shape)
                {
                    clipper.AddPath(
                        path,
                        polyType);
                }
            }
        }

        private void AddPoints(Clipper clipper, IEnumerable<IShape> shapes, PolyType polyType)
        {
            foreach (IShape shape in shapes)
            {
                this.AddPoints(clipper, shape, polyType);
            }
        }

        private void ExtractOutlines(PolyNode tree, List<IShape> shapes, List<IPath> paths)
        {
            if (tree.Contour.Any())
            {
                // if the source path is set then we clipper retained the full path intact thus we can freely
                // use it and get any shape optimisations that are availible.
                if (tree.SourcePath != null)
                {
                    shapes.Add((IShape)tree.SourcePath);
                    paths.Add(tree.SourcePath);
                }
                else
                {
                    // convert the Clipper Contour from scaled ints back down to the origional size (this is going to be lossy but not significantly)
                    Polygon polygon = new Polygon(new Paths.LinearLineSegment(tree.Contour.ToArray()));

                    shapes.Add(polygon);
                    paths.Add(polygon);
                }
            }

            foreach (PolyNode c in tree.Children)
            {
                this.ExtractOutlines(c, shapes, paths);
            }
        }

        private int FixAndSetShapes(IEnumerable<IShape> outlines, IEnumerable<IShape> holes)
        {
            Clipper clipper = new Clipper();

            // add the outlines and the holes to clipper, scaling up from the float source to the int based system clipper uses
            this.AddPoints(clipper, outlines, PolyType.Subject);
            this.AddPoints(clipper, holes, PolyType.Clip);

            PolyTree tree = clipper.Execute();

            List<IShape> shapes = new List<IShape>();
            List<IPath> paths = new List<IPath>();

            // convert the 'tree' back to paths
            this.ExtractOutlines(tree, shapes, paths);
            this.shapes = shapes.ToArray();
            this.paths = paths.ToArray();

            int intersections = 0;
            foreach (IShape s in this.shapes)
            {
                intersections += s.MaxIntersections;
            }

            return intersections;
        }
    }
}