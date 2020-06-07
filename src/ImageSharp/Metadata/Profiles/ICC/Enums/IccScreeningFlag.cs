// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Screening flags. Can be combined with a logical OR.
    /// </summary>
    [Flags]
    internal enum IccScreeningFlag : int
    {
        /// <summary>
        /// No flags (equivalent to NotDefaultScreens and UnitLinesPerCm)
        /// </summary>
        None = 0,

        /// <summary>
        /// Use printer default screens
        /// </summary>
        DefaultScreens = 1 << 0,

        /// <summary>
        /// Don't use printer default screens
        /// </summary>
        NotDefaultScreens = 0,

        /// <summary>
        /// Frequency units in Lines/Inch
        /// </summary>
        UnitLinesPerInch = 1 << 1,

        /// <summary>
        /// Frequency units in Lines/cm
        /// </summary>
        UnitLinesPerCm = 0,
    }
}
