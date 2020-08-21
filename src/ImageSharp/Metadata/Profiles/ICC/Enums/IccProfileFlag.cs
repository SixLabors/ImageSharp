// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Profile flags. Can be combined with a logical OR.
    /// The least-significant 16 bits are reserved for the ICC,
    /// the rest can be used for vendor specific values
    /// </summary>
    [Flags]
    public enum IccProfileFlag : int
    {
        /// <summary>
        /// No flags (equivalent to NotEmbedded and Independent)
        /// </summary>
        None = 0,

        /// <summary>
        /// Profile is embedded within another file
        /// </summary>
        Embedded = 1 << 0,

        /// <summary>
        /// Profile is embedded within another file
        /// </summary>
        NotEmbedded = 0,

        /// <summary>
        /// Profile cannot be used independently of the embedded color data
        /// </summary>
        NotIndependent = 1 << 1,

        /// <summary>
        /// Profile can be used independently of the embedded color data
        /// </summary>
        Independent = 0,
    }
}
