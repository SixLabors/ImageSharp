// <copyright file="BezierLineSegment.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Paths
{
    using System.Numerics;

    /// <summary>
    /// Represents a line segment that conistst of control points that will be rendered as a cubic bezier curve
    /// </summary>
    /// <seealso cref="ImageSharp.Drawing.Paths.ILineSegment" />
    public class BezierLineSegment : ILineSegment
    {
        /// <summary>
        /// The segments per curve.
        /// code for this taken from <see href="http://devmag.org.za/2011/04/05/bzier-curves-a-tutorial/"/>
        /// </summary>
        private const int SegmentsPerCurve = 50;

        /// <summary>
        /// The line points.
        /// </summary>
        private readonly Vector2[] linePoints;

        /// <summary>
        /// Initializes a new instance of the <see cref="BezierLineSegment"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        public BezierLineSegment(params Vector2[] points)
        {
            Guard.NotNull(points, nameof(points));
            Guard.MustBeGreaterThanOrEqualTo(points.Length, 4, nameof(points));

            this.linePoints = this.GetDrawingPoints(points);
        }

        /// <summary>
        /// Returns the current <see cref="ILineSegment" /> a simple linear path.
        /// </summary>
        /// <returns>
        /// Returns the current <see cref="ILineSegment" /> as simple linear path.
        /// </returns>
        public Vector2[] AsSimpleLinearPath()
        {
            return this.linePoints;
        }

        /// <summary>
        /// Returns the drawing points along the line.
        /// </summary>
        /// <param name="controlPoints">The control points.</param>
        /// <returns>
        /// The <see cref="T:Vector2[]"/>.
        /// </returns>
        private Vector2[] GetDrawingPoints(Vector2[] controlPoints)
        {
            // TODO we need to calculate an optimal SegmentsPerCurve value
            // depending on the calcualted length of this curve
            int curveCount = (controlPoints.Length - 1) / 3;
            int finalPointCount = (SegmentsPerCurve * curveCount) + 1; // we have SegmentsPerCurve for each curve plus the origon point;

            Vector2[] drawingPoints = new Vector2[finalPointCount];

            int position = 0;
            int targetPoint = controlPoints.Length - 3;
            for (int i = 0; i < targetPoint; i += 3)
            {
                Vector2 p0 = controlPoints[i];
                Vector2 p1 = controlPoints[i + 1];
                Vector2 p2 = controlPoints[i + 2];
                Vector2 p3 = controlPoints[i + 3];

                // only do this for the first end point. When i != 0, this coincides with the end point of the previous segment,
                if (i == 0)
                {
                    drawingPoints[position++] = this.CalculateBezierPoint(0, p0, p1, p2, p3);
                }

                for (int j = 1; j <= SegmentsPerCurve; j++)
                {
                    float t = j / (float)SegmentsPerCurve;
                    drawingPoints[position++] = this.CalculateBezierPoint(t, p0, p1, p2, p3);
                }
            }

            return drawingPoints;
        }

        /// <summary>
        /// Calculates the bezier point along the line.
        /// </summary>
        /// <param name="t">The position within the line.</param>
        /// <param name="p0">The p 0.</param>
        /// <param name="p1">The p 1.</param>
        /// <param name="p2">The p 2.</param>
        /// <param name="p3">The p 3.</param>
        /// <returns>
        /// The <see cref="Vector2"/>.
        /// </returns>
        private Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0; // first term

            p += 3 * uu * t * p1; // second term
            p += 3 * u * tt * p2; // third term
            p += ttt * p3; // fourth term

            return p;
        }
    }
}
