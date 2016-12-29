// <copyright file="Join.cs" company="James Jackson-South">
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
    /// ??
    /// </summary>
    internal class Join
    {
#pragma warning disable SA1401 // Field must be private
        /// <summary>
        /// The out PT1
        /// </summary>
        internal OutPt OutPt1;

        /// <summary>
        /// The out PT2
        /// </summary>
        internal OutPt OutPt2;

        /// <summary>
        /// The off pt
        /// </summary>
        internal System.Numerics.Vector2 OffPt;
#pragma warning restore SA1401 // Field must be private
    }
}