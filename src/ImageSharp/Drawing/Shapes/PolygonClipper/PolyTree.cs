// <copyright file="PolyTree.cs" company="James Jackson-South">
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
    /// Poly Tree
    /// </summary>
    /// <seealso cref="ImageSharp.Drawing.Shapes.PolygonClipper.PolyNode" />
    internal class PolyTree : PolyNode
    {
#pragma warning disable SA1401 // Field must be private
        /// <summary>
        /// All polys
        /// </summary>
        internal List<PolyNode> AllPolys = new List<PolyNode>();
#pragma warning restore SA1401 // Field must be private

        /// <summary>
        /// Gets the total.
        /// </summary>
        /// <value>
        /// The total.
        /// </value>
        public int Total
        {
            get
            {
                int result = this.AllPolys.Count;

                // with negative offsets, ignore the hidden outer polygon ...
                if (result > 0 && this.Children[0] != this.AllPolys[0])
                {
                    result--;
                }

                return result;
            }
        }

        /// <summary>
        /// Clears this instance.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < this.AllPolys.Count; i++)
            {
                this.AllPolys[i] = null;
            }

            this.AllPolys.Clear();
            this.Children.Clear();
        }

        /// <summary>
        /// Gets the first.
        /// </summary>
        /// <returns>the first node</returns>
        public PolyNode GetFirst()
        {
            if (this.Children.Count > 0)
            {
                return this.Children[0];
            }
            else
            {
                return null;
            }
        }
    }
}
