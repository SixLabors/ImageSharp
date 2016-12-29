// <copyright file="IntersectNodeSort.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Shapes.PolygonClipper
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using Paths;

    /// <summary>
    /// Compares <see cref="IntersectNode"/>s
    /// </summary>
    internal class IntersectNodeSort : IComparer<IntersectNode>
    {
        /// <summary>
        /// Compares the specified node1.
        /// </summary>
        /// <param name="node1">The node1.</param>
        /// <param name="node2">The node2.</param>
        /// <returns>
        /// 1 if node2 %gt; node1
        /// -1 if node2 $lt; node1
        /// 0 if same
        /// </returns>
        public int Compare(IntersectNode node1, IntersectNode node2)
        {
            float i = node2.Pt.Y - node1.Pt.Y;
            if (i > 0)
            {
                return 1;
            }
            else if (i < 0)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }
    }
}