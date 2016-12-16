// <copyright file="ComplexPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Shapes
{
    using System.Collections;
    using System.Collections.Generic;
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
        private IShape[] holes;
        private IShape[] outlines;
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
        /// <param name="point">The point.</param>
        /// <returns>
        /// Returns the distance from thr shape to the point
        /// </returns>
        float IShape.Distance(Vector2 point)
        {
            // get the outline we are closest to the center of
            // by rights we should only be inside 1 outline
            // othersie we will start returning the distanct to the nearest shape
            var dist = this.outlines.Select(o => o.Distance(point)).OrderBy(p => p).First();

            if (dist <= 0 && this.holes != null)
            {
                // inside poly
                foreach (var hole in this.holes)
                {
                    var distFromHole = hole.Distance(point);

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

        private void AddPoints(ClipperLib.Clipper clipper, IShape[] shapes, bool[] shouldInclude, ClipperLib.PolyType polyType)
        {
            for(var i =0; i< shapes.Length; i++)
            {
                if (shouldInclude[i])
                {
                    this.AddPoints(clipper, shapes[i], polyType);
                }
            }
        }

        
        private void ExtractOutlines(ClipperLib.PolyNode tree, List<IShape> outlines, List<IShape> holes)
        {
            if (tree.Contour.Any())
            {
                // convert the Clipper Contour from scaled ints back down to the origional size (this is going to be lossy but not significantly)
                var pointCount = tree.Contour.Count;
                var vectors = new Vector2[pointCount];
                for(var i=0; i< pointCount; i++)
                {
                    var p = tree.Contour[i];
                    vectors[i] = new Vector2(p.X, p.Y) / ClipperScaleFactor;
                }

                var polygon = new Polygon(new LinearLineSegment(vectors));

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

        /// <summary>
        /// Determines if the <see cref="IShape"/>s bounding boxes overlap.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <returns></returns>
        private bool OverlappingBoundingBoxes(IShape source, IShape target)
        {
            return source.Bounds.Intersects(target.Bounds);
        }


        private void FixAndSetShapes(IShape[] outlines, IShape[] holes)
        {
            // if any outline doesn't overlap another shape then we don't have to bother with sending them through clipper
            // as sending then though clipper will turn them into generic polygons and loose thier shape specific optimisations

            int outlineLength = outlines.Length;
            int holesLength = holes.Length;
            bool[] overlappingOutlines = new bool[outlineLength];
            bool[] overlappingHoles = new bool[holesLength];
            bool anyOutlinesOverlapping = false;
            bool anyHolesOverlapping = false;

            for (int i = 0; i < outlineLength; i++)
            {
                for (int j = i + 1; j < outlineLength; j++)
                {
                    //skip the bounds check if they are already tested
                    if (overlappingOutlines[i] == false || overlappingOutlines[j] == false)
                    {
                        if (OverlappingBoundingBoxes(outlines[i], outlines[j]))
                        {
                            overlappingOutlines[i] = true;
                            overlappingOutlines[j] = true;
                            anyOutlinesOverlapping = true;
                        }
                    }
                }

                for (int k = 0; k < holesLength; k++)
                {
                    if (overlappingOutlines[i] == false || overlappingHoles[k] == false)
                    {
                        if (OverlappingBoundingBoxes(outlines[i], holes[k]))
                        {
                            overlappingOutlines[i] = true;
                            overlappingHoles[k] = true;
                            anyOutlinesOverlapping = true;
                            anyHolesOverlapping = true;
                        }
                    }
                }
            }

            if (anyOutlinesOverlapping)
            {
                var clipper = new ClipperLib.Clipper();

                // add the outlines and the holes to clipper, scaling up from the float source to the int based system clipper uses
                this.AddPoints(clipper, outlines, overlappingOutlines, ClipperLib.PolyType.ptSubject);
                if (anyHolesOverlapping)
                {
                    this.AddPoints(clipper, holes, overlappingHoles, ClipperLib.PolyType.ptClip);
                }

                var tree = new ClipperLib.PolyTree();
                clipper.Execute(ClipperLib.ClipType.ctDifference, tree);

                List<IShape> newOutlines = new List<IShape>();
                List<IShape> newHoles = new List<IShape>();

                // convert the 'tree' back to shapes
                this.ExtractOutlines(tree, newOutlines, newHoles);

                // add the origional outlines that where not overlapping
                for (int i = 0; i < outlineLength - 1; i++)
                {
                    if (!overlappingOutlines[i])
                    {
                        newOutlines.Add(outlines[i]);
                    }
                }

                this.outlines = newOutlines.ToArray();
                if (newHoles.Count > 0)
                {
                    this.holes = newHoles.ToArray();
                }
            }else
            {
                this.outlines = outlines;
            }

            var paths = new List<IPath>();
            foreach (var o in this.outlines)
            {
                paths.AddRange(o);
            }
            if (this.holes != null)
            {
                foreach (var o in this.holes)
                {
                    paths.AddRange(o);
                }
            }
            this.paths = paths;
        }
    }
}