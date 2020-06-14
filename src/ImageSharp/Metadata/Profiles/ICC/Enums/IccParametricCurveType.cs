// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Formula curve segment type
    /// </summary>
    internal enum IccParametricCurveType : ushort
    {
        /// <summary>
        /// Type 1: Y = X^g
        /// </summary>
        Type1 = 0,

        /// <summary>
        /// CIE 122-1996:
        /// <para>For X &gt;= -b/a: Y =(a * X + b)^g</para>
        /// <para>For X $lt; -b/a: Y = 0</para>
        /// </summary>
        Cie122_1996 = 1,

        /// <summary>
        /// IEC 61966-3:
        /// <para>For X &gt;= -b/a: Y =(a * X + b)^g + c</para>
        /// <para>For X $lt; -b/a: Y = c</para>
        /// </summary>
        Iec61966_3 = 2,

        /// <summary>
        /// IEC 61966-2-1 (sRGB):
        /// <para>For X &gt;= d: Y =(a * X + b)^g</para>
        /// <para>For X $lt; d: Y = c * X</para>
        /// </summary>
        SRgb = 3,

        /// <summary>
        /// Type 5:
        /// <para>For X &gt;= d: Y =(a * X + b)^g + c</para>
        /// <para>For X $lt; d: Y = c * X + f</para>
        /// </summary>
        Type5 = 4,
    }
}
