// <copyright file="OutRec.cs" company="James Jackson-South">
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
    /// OutRec: contains a path in the clipping solution. Edges in the AEL will
    /// carry a pointer to an OutRec when they are part of the clipping solution.
    /// </summary>
    internal class OutRec
    {
#pragma warning disable SA1401 // Field must be private
        /// <summary>
        /// The source path
        /// </summary>
        internal IPath SourcePath;

        /// <summary>
        /// The index
        /// </summary>
        internal int Idx;

        /// <summary>
        /// The is hole
        /// </summary>
        internal bool IsHole;

        /// <summary>
        /// The is open
        /// </summary>
        internal bool IsOpen;

        /// <summary>
        /// The first left
        /// </summary>
        internal OutRec FirstLeft;

        /// <summary>
        /// The PTS
        /// </summary>
        internal OutPt Pts;

        /// <summary>
        /// The bottom pt
        /// </summary>
        internal OutPt BottomPt;

        /// <summary>
        /// The poly node
        /// </summary>
        internal PolyNode PolyNode;
#pragma warning restore SA1401 // Field must be private
    }
}