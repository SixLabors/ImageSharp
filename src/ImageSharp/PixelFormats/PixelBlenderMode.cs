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
        /// The default composition mode.
        /// </summary>
        /// <remarks> uses PremultipliedLerpTransform </remarks>
        Default = 0,

        /// <summary>
        /// Normal transform.
        /// </summary>
        Normal,

        /// <summary>
        /// Multiply Transform.
        /// </summary>
        Multiply,

        /// <summary>
        /// Screen Transform.
        /// </summary>
        Screen,

        /// <summary>
        /// HardLight Transform.
        /// </summary>
        HardLight,

        /// <summary>
        /// Overlay Transform.
        /// </summary>
        Overlay,

        /// <summary>
        /// Darken Transform.
        /// </summary>
        Darken,

        /// <summary>
        /// Lighten Transform.
        /// </summary>
        Lighten,

        /// <summary>
        /// SoftLight Transform.
        /// </summary>
        SoftLight,

        /// <summary>
        /// Dodge Transform.
        /// </summary>
        Dodge,

        /// <summary>
        /// Burn Transform.
        /// </summary>
        Burn,

        /// <summary>
        /// Difference Transform.
        /// </summary>
        Difference,

        /// <summary>
        /// Exclusion Transform.
        /// </summary>
        Exclusion
    }
}
