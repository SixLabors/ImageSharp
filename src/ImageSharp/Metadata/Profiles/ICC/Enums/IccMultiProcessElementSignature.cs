// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Multi process element signature
    /// </summary>
    internal enum IccMultiProcessElementSignature : uint
    {
        /// <summary>
        /// Set of curves
        /// </summary>
        CurveSet = 0x6D666C74,  // cvst

        /// <summary>
        /// Matrix transformation
        /// </summary>
        Matrix = 0x6D617466,    // matf

        /// <summary>
        /// Color lookup table
        /// </summary>
        Clut = 0x636C7574,      // clut

        /// <summary>
        /// Reserved for future expansion. Do not use!
        /// </summary>
        BAcs = 0x62414353,      // bACS

        /// <summary>
        /// Reserved for future expansion. Do not use!
        /// </summary>
        EAcs = 0x65414353,      // eACS
    }
}
