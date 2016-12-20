// <copyright file="TEdge.cs" company="James Jackson-South">
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
    /// TEdge
    /// </summary>
    internal class TEdge
    {
#pragma warning disable SA1401 // Field must be private
        /// <summary>
        /// The source path, see if we can link this back later
        /// </summary>
        internal IPath SourcePath;

        /// <summary>
        /// The bot
        /// </summary>
        internal System.Numerics.Vector2 Bot;

        /// <summary>
        /// The current (updated for every new scanbeam)
        /// </summary>
        internal System.Numerics.Vector2 Curr;

        /// <summary>
        /// The top
        /// </summary>
        internal System.Numerics.Vector2 Top;

        /// <summary>
        /// The delta
        /// </summary>
        internal System.Numerics.Vector2 Delta;

        /// <summary>
        /// The dx
        /// </summary>
        internal double Dx;

        /// <summary>
        /// The poly type
        /// </summary>
        internal PolyType PolyTyp;

        /// <summary>
        /// Side only refers to current side of solution poly
        /// </summary>
        internal EdgeSide Side;

        /// <summary>
        ///  1 or -1 depending on winding direction
        /// </summary>
        internal int WindDelta;

        /// <summary>
        /// The winding count
        /// </summary>
        internal int WindCnt;

        /// <summary>
        /// The winding count of the opposite polytype
        /// </summary>
        internal int WindCnt2;

        /// <summary>
        /// The out index
        /// </summary>
        internal int OutIdx;

        /// <summary>
        /// The next
        /// </summary>
        internal TEdge Next;

        /// <summary>
        /// The previous
        /// </summary>
        internal TEdge Prev;

        /// <summary>
        /// The next in LML
        /// </summary>
        internal TEdge NextInLML;

        /// <summary>
        /// The next in ael
        /// </summary>
        internal TEdge NextInAEL;

        /// <summary>
        /// The previous in ael
        /// </summary>
        internal TEdge PrevInAEL;

        /// <summary>
        /// The next in sel
        /// </summary>
        internal TEdge NextInSEL;

        /// <summary>
        /// The previous in sel
        /// </summary>
        internal TEdge PrevInSEL;
#pragma warning restore SA1401 // Field must be
    }
}