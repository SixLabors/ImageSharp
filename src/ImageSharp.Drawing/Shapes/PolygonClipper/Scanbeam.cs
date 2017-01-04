// <copyright file="Scanbeam.cs" company="James Jackson-South">
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
    /// Scanbeam
    /// </summary>
    internal class Scanbeam // would this work as a struct?
    {
#pragma warning disable SA1401 // Field must be private
        /// <summary>
        /// The y
        /// </summary>
        internal float Y;

        /// <summary>
        /// The next
        /// </summary>
        internal Scanbeam Next;
#pragma warning restore SA1401 // Field must be private
    }
}