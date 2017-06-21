// <copyright file="PixelCompositor{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// The various blending modes.
    /// </summary>
    public enum PixelBlenderMode
    {
        /// <summary>
        /// Default blending mode, also known as "Normal" or "Alpha Blending"
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Blends the 2 values by multiplication.
        /// </summary>
        Multiply,

        /// <summary>
        /// Blends the 2 values by addition.
        /// </summary>
        Add,

        /// <summary>
        /// Blends the 2 values by subtraction.
        /// </summary>
        Substract,

        /// <summary>
        /// Multiplies the complements of the backdrop and source values, then complements the result.
        /// </summary>
        Screen,

        /// <summary>
        /// Selects the minimum of the backdrop and source values.
        /// </summary>
        Darken,

        /// <summary>
        /// Selects the max of the backdrop and source values.
        /// </summary>
        Lighten,

        /// <summary>
        /// Multiplies or screens the values, depending on the backdrop vector values.
        /// </summary>
        Overlay,

        /// <summary>
        /// Multiplies or screens the colors, depending on the source value.
        /// </summary>
        HardLight,

        /// <summary>
        /// returns the source colors
        /// </summary>
        Src,

        /// <summary>
        /// returns the source over the destination
        /// </summary>
        Atop,

        /// <summary>
        /// returns the detination over the source
        /// </summary>
        Over,

        /// <summary>
        /// the source where the desitnation and source overlap
        /// </summary>
        In,

        /// <summary>
        /// the destination where the desitnation and source overlap
        /// </summary>
        Out,

        /// <summary>
        /// the destination where the source does not overlap it
        /// </summary>
        Dest,

        /// <summary>
        /// the source where they dont overlap othersie dest in overlapping parts
        /// </summary>
        DestAtop,

        /// <summary>
        /// the destnation over the source
        /// </summary>
        DestOver,

        /// <summary>
        /// the destination where the desitnation and source overlap
        /// </summary>
        DestIn,

        /// <summary>
        /// the source where the desitnation and source overlap
        /// </summary>
        DestOut,

        /// <summary>
        /// the clear.
        /// </summary>
        Clear,

        /// <summary>
        /// clear where they overlap
        /// </summary>
        Xor
    }
}
