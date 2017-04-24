// <copyright file="IccProfileFlag.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

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
        /// Profile cannot be used independently of the embedded colour data
        /// </summary>
        NotIndependent = 1 << 1,

        /// <summary>
        /// Profile can be used independently of the embedded colour data
        /// </summary>
        Independent = 0,
    }
}
