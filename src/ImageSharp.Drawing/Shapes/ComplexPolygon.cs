// <copyright file="ComplexPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Shapes
{
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

            this.FixAndSetShapes(outlines, holes);

            var minX = this.shapes.Min(x => x.Bounds.Left);
            var maxX = this.shapes.Max(x => x.Bounds.Right);
            var minY = this.shapes.Min(x => x.Bounds.Top);
            var maxY = this.shapes.Max(x => x.Bounds.Bottom);

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
                var d = shape.Distance(point);

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
                foreach (var path in shape)
                {
                    clipper.AddPath(
                        path,
                        polyType);
                }
            }
        }

        private void AddPoints(Clipper clipper, IEnumerable<IShape> shapes, PolyType polyType)
        {
            foreach (var shape in shapes)
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
                    var polygon = new Polygon(new Paths.LinearLineSegment(tree.Contour.ToArray()));

                    shapes.Add(polygon);
                    paths.Add(polygon);
                }
            }

            foreach (var c in tree.Children)
            {
                this.ExtractOutlines(c, shapes, paths);
            }
        }

        private void FixAndSetShapes(IEnumerable<IShape> outlines, IEnumerable<IShape> holes)
        {
            var clipper = new Clipper();

            // add the outlines and the holes to clipper, scaling up from the float source to the int based system clipper uses
            this.AddPoints(clipper, outlines, PolyType.Subject);
            this.AddPoints(clipper, holes, PolyType.Clip);

            var tree = clipper.Execute();

            List<IShape> shapes = new List<IShape>();
            List<IPath> paths = new List<IPath>();

            // convert the 'tree' back to paths
            this.ExtractOutlines(tree, shapes, paths);
            this.shapes = shapes.ToArray();
            this.paths = paths.ToArray();
        }
    }
}