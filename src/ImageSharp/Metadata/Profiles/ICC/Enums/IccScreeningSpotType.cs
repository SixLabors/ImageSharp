// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Enumerates the screening spot types
    /// </summary>
    internal enum IccScreeningSpotType : int
    {
        /// <summary>
        /// Unknown spot type
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Default printer spot type
        /// </summary>
        PrinterDefault = 1,

        /// <summary>
        /// Round stop type
        /// </summary>
        Round = 2,

        /// <summary>
        /// Diamond spot type
        /// </summary>
        Diamond = 3,

        /// <summary>
        /// Ellipse spot type
        /// </summary>
        Ellipse = 4,

        /// <summary>
        /// Line spot type
        /// </summary>
        Line = 5,

        /// <summary>
        /// Square spot type
        /// </summary>
        Square = 6,

        /// <summary>
        /// Cross spot type
        /// </summary>
        Cross = 7,
    }
}
