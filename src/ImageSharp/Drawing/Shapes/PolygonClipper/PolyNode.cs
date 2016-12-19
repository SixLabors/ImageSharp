// <copyright file="PolyNode.cs" company="James Jackson-South">
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
    /// Poly Node
    /// </summary>
    internal class PolyNode
    {
#pragma warning disable SA1401 // Field must be private
        /// <summary>
        /// The polygon
        /// </summary>
        internal List<Vector2> Polygon = new List<Vector2>();

        /// <summary>
        /// The index
        /// </summary>
        internal int Index;

        /// <summary>
        /// The childs
        /// </summary>
        protected List<PolyNode> children = new List<PolyNode>();

        private PolyNode parent;
#pragma warning restore SA1401 // Field must be private

        /// <summary>
        /// Gets the child count.
        /// </summary>
        /// <value>
        /// The child count.
        /// </value>
        public int ChildCount
        {
            get { return this.children.Count; }
        }

        /// <summary>
        /// Gets the contour.
        /// </summary>
        /// <value>
        /// The contour.
        /// </value>
        public List<Vector2> Contour
        {
            get { return this.Polygon; }
        }

        /// <summary>
        /// Gets the childs.
        /// </summary>
        /// <value>
        /// The childs.
        /// </value>
        public List<PolyNode> Children
        {
            get { return this.children; }
        }

        /// <summary>
        /// Gets or sets the parent.
        /// </summary>
        /// <value>
        /// The parent.
        /// </value>
        public PolyNode Parent
        {
            get { return this.parent; }
            internal set { this.parent = value; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is hole.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is hole; otherwise, <c>false</c>.
        /// </value>
        public bool IsHole
        {
            get { return this.IsHoleNode(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is open.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is open; otherwise, <c>false</c>.
        /// </value>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Gets or sets the source path.
        /// </summary>
        /// <value>
        /// The source path.
        /// </value>
        public IPath SourcePath { get; internal set; }

        /// <summary>
        /// Gets the next.
        /// </summary>
        /// <returns>The next node</returns>
        public PolyNode GetNext()
        {
            if (this.children.Count > 0)
            {
                return this.children[0];
            }
            else
            {
                return this.GetNextSiblingUp();
            }
        }

        /// <summary>
        /// Adds the child.
        /// </summary>
        /// <param name="child">The child.</param>
        internal void AddChild(PolyNode child)
        {
            int cnt = this.children.Count;
            this.children.Add(child);
            child.parent = this;
            child.Index = cnt;
        }

        /// <summary>
        /// Gets the next sibling up.
        /// </summary>
        /// <returns>The next sibling up</returns>
        internal PolyNode GetNextSiblingUp()
        {
            if (this.parent == null)
            {
                return null;
            }
            else if (this.Index == this.parent.children.Count - 1)
            {
                return this.parent.GetNextSiblingUp();
            }
            else
            {
                return this.parent.Children[this.Index + 1];
            }
        }

        /// <summary>
        /// Determines whether [is hole node].
        /// </summary>
        /// <returns>
        ///   <c>true</c> if [is hole node]; otherwise, <c>false</c>.
        /// </returns>
        private bool IsHoleNode()
        {
            bool result = true;
            PolyNode node = this.parent;
            while (node != null)
            {
                result = !result;
                node = node.parent;
            }

            return result;
        }
    }
}