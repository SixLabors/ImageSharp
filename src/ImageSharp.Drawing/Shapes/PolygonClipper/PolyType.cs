// <copyright file="PolyType.cs" company="James Jackson-South">
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
    /// Poly Type
    /// </summary>
    internal enum PolyType
    {
        /// <summary>
        /// The subject
        /// </summary>
        Subject,

        /// <summary>
        /// The clip
        /// </summary>
        Clip
    }
}