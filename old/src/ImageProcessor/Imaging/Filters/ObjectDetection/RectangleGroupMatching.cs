// Accord Vision Library
// The Accord.NET Framework
// http://accord-framework.net
//
// Copyright © César Souza, 2009-2015
// cesarsouza at gmail.com
//
// This code has been submitted as an user contribution by darko.juric2
// GCode Issue #12 https://code.google.com/p/accord/issues/detail?id=12
//
//    This library is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 2.1 of the License, or (at your option) any later version.
//
//    This library is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public
//    License along with this library; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
//

namespace ImageProcessor.Imaging.Filters.ObjectDetection
{
    using System;
    using System.Drawing;

    /// <summary>
    ///   Group matching algorithm for detection region averaging.
    /// </summary>
    /// 
    /// <remarks>
    ///   This class can be seen as a post-processing filter. Its goal is to
    ///   group near or contained regions together in order to produce more
    ///   robust and smooth estimates of the detected regions.
    /// </remarks>
    /// 
    public class RectangleGroupMatching : GroupMatching<Rectangle>
    {
        private double threshold;

        /// <summary>
        ///   Creates a new <see cref="RectangleGroupMatching"/> object.
        /// </summary>
        /// 
        /// <param name="minimumNeighbors">
        ///   The minimum number of neighbors needed to keep a detection. If a rectangle
        ///   has less than this minimum number, it will be discarded as a false positive.</param>
        /// <param name="threshold">
        ///   The minimum distance threshold to consider two rectangles as neighbors.
        ///   Default is 0.2.</param>
        /// 
        public RectangleGroupMatching(int minimumNeighbors = 2, double threshold = 0.2)
            : base(minimumNeighbors)
        {
            this.threshold = threshold;
        }

        /// <summary>
        ///   Gets the minimum distance threshold to consider
        ///   two rectangles as neighbors. Default is 0.2.
        /// </summary>
        /// 
        protected double Threshold
        {
            get { return threshold; }
        }

        /// <summary>
        ///   Checks if two rectangles are near.
        /// </summary>
        /// 
        protected override bool Near(Rectangle shape1, Rectangle shape2)
        {
            if (shape1.Contains(shape2) || shape2.Contains(shape1))
                return true;

            int minHeight = Math.Min(shape1.Height, shape2.Height);
            int minWidth = Math.Min(shape1.Width, shape2.Width);
            double delta = 0.5 * threshold * (minHeight + minWidth);

            return Math.Abs(shape1.X - shape2.X) <= delta
                && Math.Abs(shape1.Y - shape2.Y) <= delta
                && Math.Abs(shape1.Right - shape2.Right) <= delta
                && Math.Abs(shape1.Bottom - shape2.Bottom) <= delta;
        }

        /// <summary>
        ///   Averages rectangles which belongs to the
        ///   same class (have the same class label)
        /// </summary>
        /// 
        protected override Rectangle[] Average(int[] labels, Rectangle[] shapes, out int[] neighborCounts)
        {
            neighborCounts = new int[Classes];

            Rectangle[] centroids = new Rectangle[Classes];
            for (int i = 0; i < shapes.Length; i++)
            {
                int j = labels[i];

                centroids[j].X += shapes[i].X;
                centroids[j].Y += shapes[i].Y;
                centroids[j].Width += shapes[i].Width;
                centroids[j].Height += shapes[i].Height;

                neighborCounts[j]++;
            }

            for (int i = 0; i < centroids.Length; i++)
            {
                centroids[i] = new Rectangle(
                    x: (int)Math.Ceiling((float)centroids[i].X / neighborCounts[i]),
                    y: (int)Math.Ceiling((float)centroids[i].Y / neighborCounts[i]),
                    width: (int)Math.Ceiling((float)centroids[i].Width / neighborCounts[i]),
                    height: (int)Math.Ceiling((float)centroids[i].Height / neighborCounts[i]));
            }

            return centroids;
        }
    }

}