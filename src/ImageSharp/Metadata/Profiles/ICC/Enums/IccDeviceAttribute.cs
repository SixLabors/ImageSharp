// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Device attributes. Can be combined with a logical OR
    /// The least-significant 32 bits are defined by the ICC,
    /// the rest can be used for vendor specific values
    /// </summary>
    [Flags]
    public enum IccDeviceAttribute : long
    {
        /// <summary>
        /// Opacity transparent
        /// </summary>
        OpacityTransparent = 1 << 0,

        /// <summary>
        /// Opacity reflective
        /// </summary>
        OpacityReflective = 0,

        /// <summary>
        /// Reflectivity matte
        /// </summary>
        ReflectivityMatte = 1 << 1,

        /// <summary>
        /// Reflectivity glossy
        /// </summary>
        ReflectivityGlossy = 0,

        /// <summary>
        /// Polarity negative
        /// </summary>
        PolarityNegative = 1 << 2,

        /// <summary>
        /// Polarity positive
        /// </summary>
        PolarityPositive = 0,

        /// <summary>
        /// Chroma black and white
        /// </summary>
        ChromaBlackWhite = 1 << 3,

        /// <summary>
        /// Chroma color
        /// </summary>
        ChromaColor = 0,
    }
}
