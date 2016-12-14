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

    /// <summary>
    /// Represents a complex polygon made up of one or more outline
    /// polygons and one or more holes to punch out of them.
    /// </summary>
    /// <seealso cref="ImageSharp.Drawing.Shapes.IShape" />
    public sealed class ComplexPolygon : IShape
    {
        private const float ClipperScaleFactor = 100f;
        private IEnumerable<IShape> holes;
        private IEnumerable<IShape> outlines;
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
        public ComplexPolygon(IEnumerable<IShape> outlines, IEnumerable<IShape> holes)
        {
            Guard.NotNull(outlines, nameof(outlines));
            Guard.MustBeGreaterThanOrEqualTo(outlines.Count(), 1, nameof(outlines));

            this.FixAndSetShapes(outlines, holes);

            var minX = outlines.Min(x => x.Bounds.Left);
            var maxX = outlines.Max(x => x.Bounds.Right);
            var minY = outlines.Min(x => x.Bounds.Top);
            var maxY = outlines.Max(x => x.Bounds.Bottom);

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
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>
        /// Returns the distance from thr shape to the point
        /// </returns>
        float IShape.Distance(int x, int y)
        {
            // get the outline we are closest to the center of
            // by rights we should only be inside 1 outline
            // othersie we will start returning the distanct to the nearest shape
            var dist = this.outlines.Select(o => o.Distance(x, y)).OrderBy(p => p).First();

            if (dist <= 0)
            {
                // inside poly
                foreach (var hole in this.holes)
                {
                    var distFromHole = hole.Distance(x, y);

                    // less than zero we are inside shape
                    if (distFromHole <= 0)
                    {
                        // invert distance
                        dist = distFromHole * -1;
                        break;
                    }
                }
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

        private void AddPoints(ClipperLib.Clipper clipper, IShape shape, ClipperLib.PolyType polyType)
        {
            foreach (var path in shape)
            {
                var points = path.AsSimpleLinearPath();
                var clipperPoints = new List<ClipperLib.IntPoint>();
                foreach (var point in points)
                {
                    var p = point * ClipperScaleFactor;

                    clipperPoints.Add(new ClipperLib.IntPoint((long)p.X, (long)p.Y));
                }

                clipper.AddPath(
                    clipperPoints,
                    polyType,
                    path.IsClosed);
            }
        }

        private void AddPoints(ClipperLib.Clipper clipper, IEnumerable<IShape> shapes, ClipperLib.PolyType polyType)
        {
            foreach (var shape in shapes)
            {
                this.AddPoints(clipper, shape, polyType);
            }
        }

        private void ExtractOutlines(ClipperLib.PolyNode tree, List<Polygon> outlines, List<Polygon> holes)
        {
            if (tree.Contour.Any())
            {
                // convert the Clipper Contour from scaled ints back down to the origional size (this is going to be lossy but not significantly)
                var polygon = new Polygon(new LinearLineSegment(tree.Contour.Select(x => new Vector2(x.X / ClipperScaleFactor, x.Y / ClipperScaleFactor)).ToArray()));

                if (tree.IsHole)
                {
                    holes.Add(polygon);
                }
                else
                {
                    outlines.Add(polygon);
                }
            }

            foreach (var c in tree.Childs)
            {
                this.ExtractOutlines(c, outlines, holes);
            }
        }

        private void FixAndSetShapes(IEnumerable<IShape> outlines, IEnumerable<IShape> holes)
        {
            var clipper = new ClipperLib.Clipper();

            // add the outlines and the holes to clipper, scaling up from the float source to the int based system clipper uses
            this.AddPoints(clipper, outlines, ClipperLib.PolyType.ptSubject);
            this.AddPoints(clipper, holes, ClipperLib.PolyType.ptClip);

            var tree = new ClipperLib.PolyTree();
            clipper.Execute(ClipperLib.ClipType.ctDifference, tree);

            List<Polygon> newOutlines = new List<Polygon>();
            List<Polygon> newHoles = new List<Polygon>();

            // convert the 'tree' back to paths
            this.ExtractOutlines(tree, newOutlines, newHoles);

            this.outlines = newOutlines;
            this.holes = newHoles;

            // extract the final list of paths out of the new polygons we just converted down to.
            this.paths = newOutlines.Union(newHoles).ToArray();
        }
    }
}